using Coiny.API.Data;
using Coiny.API.DTOs.Transactions;
using Coiny.API.Enums;
using Coiny.API.Interfaces;
using Coiny.API.Models;
using Coiny.API.Services;
using Microsoft.EntityFrameworkCore;

namespace Coiny.API.Tests;

public class TransferServiceTests
{
    private static readonly DateOnly Day = new(2026, 10, 1);
    private sealed class User(int householdId = 1) : ICurrentUserService
    {
        public string? GetUserId() => "test";
        public bool IsAuthenticated() => true;
        public Task<int> GetCurrentHouseholdIdAsync() => Task.FromResult(householdId);
    }
    private static async Task Seed(CoinyContext context)
    {
        context.Accounts.AddRange(
            new Account { Id = 1, HouseholdId = 1, Name = "Bills", AccountType = AccountType.Checking, OpeningBalance = 1000, OpeningBalanceDate = Day },
            new Account { Id = 2, HouseholdId = 1, Name = "Spending", AccountType = AccountType.Checking, OpeningBalance = 100, OpeningBalanceDate = Day },
            new Account { Id = 3, HouseholdId = 1, Name = "Card", AccountType = AccountType.CreditCard, OpeningBalance = 700, OpeningBalanceDate = Day },
            new Account { Id = 4, HouseholdId = 2, Name = "Foreign", OpeningBalanceDate = Day });
        await context.SaveChangesAsync();
    }
    private static TransferService Service(CoinyContext c) => new(c, new User(), new AccountBalanceSnapshotService(c, new User()));
    private static CreateTransferRequest Pair(int to = 2) => new() { FromAccountId = 1, ToAccountId = to, Amount = 250, Date = Day };
    private static CreateTransferEntryRequest Entry(int account, TransferDirection direction, DateOnly? date = null) => new()
    { AccountId = account, Direction = direction, Amount = 250, Date = date ?? Day, Description = "Bank transfer" };

    [Fact]
    public async Task Candidates_prioritize_same_day_and_exclude_wrong_amount_direction_account_and_linked_entries()
    {
        await using var db = await BudgetTestDatabase.CreateAsync(); var c = db.Context(); await Seed(c); var service = Service(c);
        var source = await service.CreateEntryAsync(Entry(1, TransferDirection.Out));
        var nearby = await service.CreateEntryAsync(Entry(2, TransferDirection.In, Day.AddDays(3)));
        var sameDay = await service.CreateEntryAsync(Entry(2, TransferDirection.In));
        var sameDayAgain = await service.CreateEntryAsync(Entry(2, TransferDirection.In));
        await service.CreateEntryAsync(Entry(2, TransferDirection.Out));
        var wrongAmount = Entry(2, TransferDirection.In); wrongAmount.Amount = 249; await service.CreateEntryAsync(wrongAmount);
        await service.CreateEntryAsync(Entry(3, TransferDirection.In));
        await service.CreateEntryAsync(Entry(2, TransferDirection.In, Day.AddDays(8)));
        var newPair = Pair(); newPair.CreateBothEntries = true;
        await service.CreateAsync(newPair);
        var rows = await service.GetCandidatesAsync(source.Id, 2, 7);
        Assert.Equal(new[] { sameDay.Id, sameDayAgain.Id, nearby.Id }, rows.Select(r => r.Id));
        Assert.Equal(2, (await service.GetCandidatesAsync(source.Id, 2, 0)).Count);
        Assert.Equal(4, (await service.GetCandidatesAsync(source.Id, 2, 30)).Count);
        await Assert.ThrowsAsync<TransferNotFoundException>(() => service.GetCandidatesAsync(source.Id, 4, 7));
        await Assert.ThrowsAsync<ArgumentException>(() => service.GetCandidatesAsync(source.Id, 1, 7));
        await Assert.ThrowsAsync<ArgumentException>(() => service.GetCandidatesAsync(source.Id, 2, 366));
        var foreign = new TransferService(c, new User(2), new AccountBalanceSnapshotService(c,new User(2)));
        await Assert.ThrowsAsync<TransferNotFoundException>(() => foreign.GetCandidatesAsync(source.Id, 2, 7));
    }

    [Fact]
    public async Task Reviewed_link_and_unlink_preserve_both_ledgers_dates_fields_and_snapshots()
    {
        await using var db = await BudgetTestDatabase.CreateAsync(); var c = db.Context(); await Seed(c); var service = Service(c);
        var first = await service.CreateEntryAsync(Entry(1, TransferDirection.Out));
        var request = Entry(2, TransferDirection.In, Day.AddDays(2)); request.Description = "Bank posted deposit"; request.Notes = "Keep this note"; request.IsCleared = true;
        var second = await service.CreateEntryAsync(request); var balances = new AccountBalanceService(c);
        var before = new[] { await balances.GetCurrentBalanceAsync(1), await balances.GetCurrentBalanceAsync(2) };
        var snapshots = await c.AccountBalanceSnapshots.AsNoTracking().OrderBy(s => s.Id).Select(s => new { s.AccountId, s.SnapshotDate, s.Balance }).ToArrayAsync();
        var linked = await service.LinkAsync(new() { FirstTransactionId = first.Id, SecondTransactionId = second.Id,
            ExpectedFirstEditVersion = first.EditVersion, ExpectedSecondEditVersion = second.EditVersion });
        Assert.Equal(2, await c.Transactions.CountAsync());
        Assert.Equal(second.TransactionDate, linked.Incoming.TransactionDate); Assert.Equal(second.Description, linked.Incoming.Description);
        Assert.Equal(second.Notes, linked.Incoming.Notes); Assert.True(linked.Incoming.IsCleared);
        await service.UnlinkAsync(first.Id, linked.Outgoing.EditVersion, linked.Incoming.EditVersion);
        Assert.Equal(before, new[] { await balances.GetCurrentBalanceAsync(1), await balances.GetCurrentBalanceAsync(2) });
        Assert.Equal(snapshots, await c.AccountBalanceSnapshots.AsNoTracking().OrderBy(s => s.Id).Select(s => new { s.AccountId, s.SnapshotDate, s.Balance }).ToArrayAsync());
        Assert.Equal(2, await c.Transactions.CountAsync());
    }

    [Fact]
    public async Task Version_checks_reject_changed_amounts_before_link_create_or_unlink()
    {
        await using var db = await BudgetTestDatabase.CreateAsync(); var c = db.Context(); await Seed(c); var service = Service(c);
        var first = await service.CreateEntryAsync(Entry(1, TransferDirection.Out)); var second = await service.CreateEntryAsync(Entry(2, TransferDirection.In));
        var entry = await c.Transactions.SingleAsync(t => t.Id == second.Id); entry.EditVersion++; await c.SaveChangesAsync();
        await Assert.ThrowsAsync<TransferConflictException>(() => service.LinkAsync(new() { FirstTransactionId = first.Id, SecondTransactionId = second.Id, ExpectedFirstEditVersion = first.EditVersion, ExpectedSecondEditVersion = second.EditVersion }));
        var original = await c.Transactions.SingleAsync(t => t.Id == first.Id); original.EditVersion++; await c.SaveChangesAsync();
        await Assert.ThrowsAsync<TransferConflictException>(() => service.CreateCounterpartAsync(first.Id, new() { AccountId = 2, Date = Day, ExpectedEditVersion = first.EditVersion }));
        var linked = await service.LinkAsync(new() { FirstTransactionId = first.Id, SecondTransactionId = second.Id, ExpectedFirstEditVersion = original.EditVersion, ExpectedSecondEditVersion = entry.EditVersion });
        await Assert.ThrowsAsync<TransferConflictException>(() => service.UnlinkAsync(first.Id, first.EditVersion, linked.Incoming.EditVersion));
        Assert.Equal(2, await c.Transactions.CountAsync()); Assert.NotNull((await c.Transactions.SingleAsync(t => t.Id == first.Id)).PairedTransactionId);
    }

    [Fact]
    public async Task Explicit_counterpart_creation_adds_only_one_entry_and_leaves_original_balance_unchanged()
    {
        await using var db = await BudgetTestDatabase.CreateAsync(); var c = db.Context(); await Seed(c); var service = Service(c);
        var source = await service.CreateEntryAsync(Entry(1, TransferDirection.Out));
        var originalSnapshot = await c.AccountBalanceSnapshots.SingleAsync(s => s.AccountId == 1);
        var pair = await service.CreateCounterpartAsync(source.Id, new() { AccountId = 3, Date = Day.AddDays(2), ExpectedEditVersion = source.EditVersion });
        Assert.Equal(source.Id, pair.Outgoing.Id); Assert.Equal(source.TransactionDate, pair.Outgoing.TransactionDate);
        Assert.Equal(Day.AddDays(2).ToDateTime(TimeOnly.MinValue), pair.Incoming.TransactionDate); Assert.False(pair.Incoming.IsCleared);
        Assert.Equal(2, await c.Transactions.CountAsync()); Assert.Equal(750, await new AccountBalanceService(c).GetCurrentBalanceAsync(1));
        Assert.Equal(450, await new AccountBalanceService(c).GetCurrentBalanceAsync(3)); Assert.Equal(750, originalSnapshot.Balance);
        await Assert.ThrowsAsync<TransferConflictException>(() => service.CreateCounterpartAsync(source.Id, new() { AccountId = 3, Date = Day, ExpectedEditVersion = source.EditVersion }));
        Assert.Equal(2, await c.Transactions.CountAsync());
    }

    [Fact]
    public async Task Counterpart_failure_rolls_back_new_entry_link_and_snapshots()
    {
        await using var db = await BudgetTestDatabase.CreateAsync(); var c = db.Context(); await Seed(c); var service = Service(c);
        var source = await service.CreateEntryAsync(Entry(1, TransferDirection.Out));
        await c.Database.ExecuteSqlRawAsync("CREATE TRIGGER reject_counterpart_snapshot BEFORE INSERT ON AccountBalanceSnapshots WHEN NEW.AccountId = 2 BEGIN SELECT RAISE(ABORT, 'test failure'); END;");
        await Assert.ThrowsAsync<DbUpdateException>(() => service.CreateCounterpartAsync(source.Id, new() { AccountId = 2, Date = Day, ExpectedEditVersion = source.EditVersion }));
        using var verify = db.Context(); Assert.Equal(1, await verify.Transactions.CountAsync());
        Assert.Null((await verify.Transactions.SingleAsync()).PairedTransactionId); Assert.Equal(0, (await verify.Transactions.SingleAsync()).EditVersion);
        Assert.Single(await verify.AccountBalanceSnapshots.ToListAsync());
    }

    [Fact]
    public async Task Counterpart_creation_rejects_foreign_closed_same_and_early_accounts()
    {
        await using var db = await BudgetTestDatabase.CreateAsync(); var c = db.Context(); await Seed(c); var service = Service(c);
        var source = await service.CreateEntryAsync(Entry(1, TransferDirection.Out));
        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateCounterpartAsync(source.Id,new() { AccountId = 1, Date = Day, ExpectedEditVersion = source.EditVersion }));
        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateCounterpartAsync(source.Id,new() { AccountId = 4, Date = Day, ExpectedEditVersion = source.EditVersion }));
        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateCounterpartAsync(source.Id,new() { AccountId = 2, Date = Day.AddDays(-1), ExpectedEditVersion = source.EditVersion }));
        (await c.Accounts.FindAsync(2))!.IsActive = false; await c.SaveChangesAsync();
        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateCounterpartAsync(source.Id,new() { AccountId = 2, Date = Day, ExpectedEditVersion = source.EditVersion }));
        Assert.Single(await c.Transactions.ToListAsync());
    }

    [Fact]
    public async Task Manual_transfer_updates_both_ledgers_and_snapshots_and_preserves_net_worth()
    {
        await using var db = await BudgetTestDatabase.CreateAsync(); var c = db.Context(); await Seed(c);
        var pair = await Service(c).CreateAsync(Pair());
        Assert.Equal(pair.Incoming.Id, pair.Outgoing.PairedTransactionId);
        Assert.Equal(pair.Outgoing.Id, pair.Incoming.PairedTransactionId);
        Assert.Equal("Spending", pair.Outgoing.CounterpartyAccountName);
        Assert.Equal(AccountType.Checking, pair.Incoming.AccountType);
        Assert.Equal(AccountType.Checking, pair.Incoming.CounterpartyAccountType);
        Assert.Equal(TransferDirection.Out, pair.Outgoing.TransferDirection);
        var balances = new AccountBalanceService(c);
        Assert.Equal(750, await balances.GetCurrentBalanceAsync(1));
        Assert.Equal(350, await balances.GetCurrentBalanceAsync(2));
        Assert.Equal(1100, await c.AccountBalanceSnapshots.SumAsync(s => s.Balance));
        var transactions = new TransactionService(c, new User(), new AccountBalanceSnapshotService(c, new User()));
        var listed = await transactions.GetTransactionsAsync();
        Assert.All(listed, t => Assert.NotNull(t.PairedTransactionId));
        foreach (var type in new[] { CategoryType.Income, CategoryType.Expense })
            Assert.Equal(0, (await transactions.GetCategoryBreakdownAsync(new() { StartDate = Day, EndDate = Day, CategoryType = type })).TotalAmount);
    }

    [Fact]
    public async Task Credit_card_payment_reduces_cash_and_amount_owed()
    {
        await using var db = await BudgetTestDatabase.CreateAsync(); var c = db.Context(); await Seed(c);
        var payment = await Service(c).CreateAsync(Pair(3));
        Assert.Equal(AccountType.CreditCard, payment.Incoming.AccountType);
        Assert.Equal(AccountType.Checking, payment.Incoming.CounterpartyAccountType);
        Assert.Equal(750, await new AccountBalanceService(c).GetCurrentBalanceAsync(1));
        Assert.Equal(450, await new AccountBalanceService(c).GetCurrentBalanceAsync(3));
        Assert.Equal(300, await new AccountBalanceService(c).GetCurrentBalanceAsync(1) - await new AccountBalanceService(c).GetCurrentBalanceAsync(3));
    }

    [Fact]
    public async Task Link_and_unlink_preserve_dates_balances_and_snapshots()
    {
        await using var db = await BudgetTestDatabase.CreateAsync(); var c = db.Context(); await Seed(c);
        var service = Service(c);
        var outgoing = await service.CreateEntryAsync(Entry(1, TransferDirection.Out));
        var incoming = await service.CreateEntryAsync(Entry(2, TransferDirection.In, Day.AddDays(3)));
        Assert.Equal(TransferCounterparty.Unknown, outgoing.TransferCounterparty);
        var before = await c.AccountBalanceSnapshots.OrderBy(s => s.Id).Select(s => s.Balance).ToArrayAsync();
        var linked = await service.LinkAsync(new() { FirstTransactionId = incoming.Id, SecondTransactionId = outgoing.Id });
        Assert.Equal(Day.ToDateTime(TimeOnly.MinValue), linked.Outgoing.TransactionDate);
        Assert.Equal(Day.AddDays(3).ToDateTime(TimeOnly.MinValue), linked.Incoming.TransactionDate);
        await Assert.ThrowsAsync<TransferConflictException>(() => service.LinkAsync(new() { FirstTransactionId = incoming.Id, SecondTransactionId = outgoing.Id }));
        await service.UnlinkAsync(outgoing.Id);
        Assert.All(await c.Transactions.ToListAsync(), t => { Assert.Null(t.PairedTransactionId); Assert.Equal(TransferCounterparty.Unknown, t.TransferCounterparty); });
        Assert.Equal(before, await c.AccountBalanceSnapshots.OrderBy(s => s.Id).Select(s => s.Balance).ToArrayAsync());
        Assert.Equal(2, await c.Transactions.CountAsync());
    }

    [Fact]
    public async Task Invalid_accounts_dates_precision_and_link_pairs_are_rejected()
    {
        await using var db = await BudgetTestDatabase.CreateAsync(); var c = db.Context(); await Seed(c);
        var service = Service(c);
        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateAsync(Pair(1)));
        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateAsync(Pair(4)));
        var early = Pair(); early.Date = Day.AddDays(-1);
        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateAsync(early));
        var precision = Pair(); precision.Amount = 0.001m;
        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateAsync(precision));
        Assert.Equal(0, await c.Transactions.CountAsync());
        var first = await service.CreateEntryAsync(Entry(1, TransferDirection.Out));
        var sameDirection = await service.CreateEntryAsync(Entry(2, TransferDirection.Out));
        await Assert.ThrowsAsync<ArgumentException>(() => service.LinkAsync(new() { FirstTransactionId = first.Id, SecondTransactionId = sameDirection.Id }));
        await Assert.ThrowsAsync<TransferNotFoundException>(() => service.UnlinkAsync(999));
        var foreign = new TransferService(c, new User(2), new AccountBalanceSnapshotService(c, new User(2)));
        await Assert.ThrowsAsync<TransferNotFoundException>(() => foreign.LinkAsync(new() { FirstTransactionId = first.Id, SecondTransactionId = sameDirection.Id }));
    }

    [Fact]
    public async Task External_entry_changes_only_its_account_and_recalculates_later_snapshots()
    {
        await using var db = await BudgetTestDatabase.CreateAsync(); var c = db.Context(); await Seed(c);
        await Service(c).CreateAsync(new() { FromAccountId = 1, ToAccountId = 2, Amount = 10, Date = Day.AddDays(5) });
        var request = Entry(1, TransferDirection.Out); request.Counterparty = TransferCounterparty.External;
        var entry = await Service(c).CreateEntryAsync(request);
        Assert.Equal(TransferCounterparty.External, entry.TransferCounterparty);
        Assert.Null(entry.PairedTransactionId);
        Assert.Equal(3, await c.Transactions.CountAsync());
        Assert.Equal(740, await c.AccountBalanceSnapshots.Where(s => s.AccountId == 1 && s.SnapshotDate == Day.AddDays(5)).Select(s => s.Balance).SingleAsync());
    }

    [Fact]
    public async Task Failed_second_snapshot_rolls_back_both_entries_and_first_snapshot()
    {
        await using var db = await BudgetTestDatabase.CreateAsync(); var c = db.Context(); await Seed(c);
        await c.Database.ExecuteSqlRawAsync("CREATE TRIGGER reject_second_snapshot BEFORE INSERT ON AccountBalanceSnapshots WHEN NEW.AccountId = 2 BEGIN SELECT RAISE(ABORT, 'test failure'); END;");
        await Assert.ThrowsAsync<DbUpdateException>(() => Service(c).CreateAsync(Pair()));
        var verify = db.Context();
        Assert.Equal(0, await verify.Transactions.CountAsync());
        Assert.Equal(0, await verify.AccountBalanceSnapshots.CountAsync());
        Assert.Equal(0, await verify.Categories.CountAsync());
    }

    [Fact]
    public async Task Stale_link_attempt_cannot_overwrite_an_existing_pair()
    {
        await using var db = await BudgetTestDatabase.CreateAsync(); var c = db.Context(); await Seed(c);
        var service = Service(c);
        var outgoing = await service.CreateEntryAsync(Entry(1, TransferDirection.Out));
        var incoming = await service.CreateEntryAsync(Entry(2, TransferDirection.In));
        var anotherIncoming = await service.CreateEntryAsync(Entry(3, TransferDirection.In));
        var stale = db.Context();
        await stale.Transactions.ToListAsync();
        await service.LinkAsync(new() { FirstTransactionId = outgoing.Id, SecondTransactionId = incoming.Id });
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => Service(stale).LinkAsync(new()
        { FirstTransactionId = outgoing.Id, SecondTransactionId = anotherIncoming.Id }));
        var verify = db.Context();
        Assert.Equal(incoming.Id, (await verify.Transactions.SingleAsync(t => t.Id == outgoing.Id)).PairedTransactionId);
        Assert.Null((await verify.Transactions.SingleAsync(t => t.Id == anotherIncoming.Id)).PairedTransactionId);
    }

    [Fact]
    public async Task Same_account_unequal_amount_and_self_matches_are_rejected_without_changes()
    {
        await using var db = await BudgetTestDatabase.CreateAsync(); var c = db.Context(); await Seed(c);
        var service = Service(c);
        var outgoing = await service.CreateEntryAsync(Entry(1, TransferDirection.Out));
        var incoming = await service.CreateEntryAsync(Entry(1, TransferDirection.In));
        await Assert.ThrowsAsync<ArgumentException>(() => service.LinkAsync(new() { FirstTransactionId = outgoing.Id, SecondTransactionId = incoming.Id }));
        await Assert.ThrowsAsync<ArgumentException>(() => service.LinkAsync(new() { FirstTransactionId = outgoing.Id, SecondTransactionId = outgoing.Id }));
        var different = Entry(2, TransferDirection.In); different.Amount = 249;
        var mismatch = await service.CreateEntryAsync(different);
        await Assert.ThrowsAsync<ArgumentException>(() => service.LinkAsync(new() { FirstTransactionId = outgoing.Id, SecondTransactionId = mismatch.Id }));
        Assert.All(await c.Transactions.ToListAsync(), t => Assert.Null(t.PairedTransactionId));
    }

    [Fact]
    public async Task Creating_a_payment_links_imported_card_entry_and_changes_only_the_missing_bills_side()
    {
        await using var db = await BudgetTestDatabase.CreateAsync(); var c = db.Context(); await Seed(c); var service = Service(c);
        var imported = Entry(3, TransferDirection.In); imported.IsCleared = true; imported.Notes = "Imported note"; imported.Description = "Bank card payment";
        var existing = await service.CreateEntryAsync(imported);
        var batch = new TransactionImportBatch { HouseholdId = 1, AccountId = 3, Filename = "card.csv", SubmissionId = Guid.NewGuid(), PayloadHash = new('a', 64), ImportedCount = 1, CreatedByUserId = "test" };
        c.TransactionImportBatches.Add(batch); await c.SaveChangesAsync();
        var original = await c.Transactions.SingleAsync(t => t.Id == existing.Id); original.ImportBatchId = batch.Id; original.ImportRowId = 5; original.Merchant = "Apple"; await c.SaveChangesAsync();
        var cardSnapshots = await c.AccountBalanceSnapshots.AsNoTracking().Where(s => s.AccountId == 3).Select(s => new { s.SnapshotDate, s.Balance }).ToArrayAsync();
        var request = Pair(3); request.Date = Day.AddDays(2); request.Description = "New Bills entry"; request.Notes = "New note"; request.IsCleared = false;
        var matches = await service.GetCreationCandidatesAsync(request); Assert.Equal(existing.Id, Assert.Single(matches).Id);
        await Assert.ThrowsAsync<TransferConflictException>(() => service.CreateAsync(request));
        Assert.Single(await c.Transactions.ToListAsync());
        request.ExistingTransactionId = existing.Id; request.ExpectedExistingEditVersion = existing.EditVersion;
        var pair = await service.CreateAsync(request);
        Assert.Equal(existing.Id, pair.Incoming.Id); Assert.Equal(existing.TransactionDate, pair.Incoming.TransactionDate); Assert.Equal("Bank card payment", pair.Incoming.Description);
        Assert.Equal("Imported note", pair.Incoming.Notes); Assert.True(pair.Incoming.IsCleared); Assert.Equal("Apple", pair.Incoming.Merchant);
        Assert.Equal(batch.Id, pair.Incoming.ImportBatchId); Assert.Equal(5, (await c.Transactions.FindAsync(existing.Id))!.ImportRowId);
        Assert.Equal(Day.AddDays(2).ToDateTime(TimeOnly.MinValue), pair.Outgoing.TransactionDate); Assert.Equal("New Bills entry", pair.Outgoing.Description); Assert.False(pair.Outgoing.IsCleared);
        Assert.Equal(2, await c.Transactions.CountAsync()); Assert.Equal(750, await new AccountBalanceService(c).GetCurrentBalanceAsync(1)); Assert.Equal(450, await new AccountBalanceService(c).GetCurrentBalanceAsync(3));
        Assert.Equal(cardSnapshots, await c.AccountBalanceSnapshots.AsNoTracking().Where(s => s.AccountId == 3).Select(s => new { s.SnapshotDate, s.Balance }).ToArrayAsync());
        await Assert.ThrowsAsync<TransferConflictException>(() => service.CreateAsync(request)); Assert.Equal(2, await c.Transactions.CountAsync());
    }

    [Fact]
    public async Task Creation_matches_both_sides_but_excludes_wrong_directions_amounts_dates_accounts_and_linked_rows()
    {
        await using var db = await BudgetTestDatabase.CreateAsync(); var c = db.Context(); await Seed(c); var service = Service(c);
        var outgoing = await service.CreateEntryAsync(Entry(1, TransferDirection.Out, Day.AddDays(2)));
        var incoming = await service.CreateEntryAsync(Entry(3, TransferDirection.In));
        await service.CreateEntryAsync(Entry(1, TransferDirection.In)); await service.CreateEntryAsync(Entry(3, TransferDirection.Out));
        await service.CreateEntryAsync(Entry(2, TransferDirection.In)); await service.CreateEntryAsync(Entry(3, TransferDirection.In, Day.AddDays(8)));
        var wrong = Entry(3, TransferDirection.In); wrong.Amount = 249; await service.CreateEntryAsync(wrong);
        var request = Pair(3); request.CreateBothEntries = true; await service.CreateAsync(request);
        var matches = await service.GetCreationCandidatesAsync(request);
        Assert.Equal(new[] { incoming.Id, outgoing.Id }, matches.Select(t => t.Id));
        request.ToAccountId = 4; await Assert.ThrowsAsync<ArgumentException>(() => service.GetCreationCandidatesAsync(request));
        var foreign = new TransferService(c, new User(2), new AccountBalanceSnapshotService(c, new User(2)));
        await Assert.ThrowsAsync<ArgumentException>(() => foreign.GetCreationCandidatesAsync(Pair(3)));
    }

    [Fact]
    public async Task Creation_can_reuse_outgoing_entry_and_only_checks_opening_date_for_new_incoming_entry()
    {
        await using var db = await BudgetTestDatabase.CreateAsync(); var c = db.Context(); await Seed(c); var service = Service(c);
        (await c.Accounts.FindAsync(1))!.OpeningBalanceDate = Day.AddDays(2); await c.SaveChangesAsync();
        var outgoing = await service.CreateEntryAsync(Entry(1, TransferDirection.Out, Day.AddDays(2)));
        var request = Pair(); request.ExistingTransactionId = outgoing.Id; request.ExpectedExistingEditVersion = outgoing.EditVersion;
        var pair = await service.CreateAsync(request);
        Assert.Equal(outgoing.Id, pair.Outgoing.Id); Assert.Equal(Day.AddDays(2).ToDateTime(TimeOnly.MinValue), pair.Outgoing.TransactionDate);
        Assert.Equal(Day.ToDateTime(TimeOnly.MinValue), pair.Incoming.TransactionDate);
        Assert.Equal(750, await new AccountBalanceService(c).GetCurrentBalanceAsync(1)); Assert.Equal(350, await new AccountBalanceService(c).GetCurrentBalanceAsync(2));
    }

    [Fact]
    public async Task Creation_rejects_stale_unreviewed_and_incompatible_existing_entries_without_writes()
    {
        await using var db = await BudgetTestDatabase.CreateAsync(); var c = db.Context(); await Seed(c); var service = Service(c);
        var existing = await service.CreateEntryAsync(Entry(3, TransferDirection.In));
        var request = Pair(3); request.ExistingTransactionId = existing.Id;
        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateAsync(request));
        request.ExpectedExistingEditVersion = 1; await Assert.ThrowsAsync<TransferConflictException>(() => service.CreateAsync(request));
        request.ExpectedExistingEditVersion = 0; request.Amount = 251; await Assert.ThrowsAsync<TransferConflictException>(() => service.CreateAsync(request));
        request.Amount = 250; request.ToAccountId = 2; await Assert.ThrowsAsync<TransferConflictException>(() => service.CreateAsync(request));
        request.ToAccountId = 3; request.Date = Day.AddDays(8); await Assert.ThrowsAsync<TransferConflictException>(() => service.CreateAsync(request));
        Assert.Single(await c.Transactions.ToListAsync()); Assert.Null((await c.Transactions.FindAsync(existing.Id))!.PairedTransactionId);
    }

    [Fact]
    public async Task Creation_link_and_missing_entry_roll_back_together_if_snapshot_write_fails()
    {
        await using var db = await BudgetTestDatabase.CreateAsync(); var c = db.Context(); await Seed(c); var service = Service(c);
        var existing = await service.CreateEntryAsync(Entry(3, TransferDirection.In));
        await c.Database.ExecuteSqlRawAsync("CREATE TRIGGER reject_missing_snapshot BEFORE INSERT ON AccountBalanceSnapshots WHEN NEW.AccountId = 1 BEGIN SELECT RAISE(ABORT, 'test failure'); END;");
        var request = Pair(3); request.ExistingTransactionId = existing.Id; request.ExpectedExistingEditVersion = existing.EditVersion;
        await Assert.ThrowsAsync<DbUpdateException>(() => service.CreateAsync(request));
        var verify = db.Context(); var retained = Assert.Single(await verify.Transactions.ToListAsync()); Assert.Null(retained.PairedTransactionId); Assert.Equal(0, retained.EditVersion);
        Assert.Single(await verify.AccountBalanceSnapshots.ToListAsync()); Assert.Equal(1000, await new AccountBalanceService(verify).GetCurrentBalanceAsync(1));
    }
}
