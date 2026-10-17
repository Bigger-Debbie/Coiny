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
}
