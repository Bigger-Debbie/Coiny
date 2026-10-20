using Coiny.API.Data;
using Coiny.API.DTOs.Transactions;
using Coiny.API.Enums;
using Coiny.API.Interfaces;
using Coiny.API.Models;
using Coiny.API.Services;
using Microsoft.EntityFrameworkCore;
namespace Coiny.API.Tests;
public class TransactionManagementTests
{
    private sealed class User(int id = 1) : ICurrentUserService {
        public string? GetUserId() => "test"; public bool IsAuthenticated() => true;
        public Task<int> GetCurrentHouseholdIdAsync() => Task.FromResult(id);
    }
    private static TransactionManagementService Service(BudgetTestDatabase db, int household = 1) {
        var c = db.Context(); var user = new User(household);
        return new(c, user, new AccountBalanceSnapshotService(c, user));
    }
    private static async Task Seed(BudgetTestDatabase db)
    {
        var c = db.Context();
        c.Accounts.AddRange(new Account { Id = 1, HouseholdId = 1, Name = "Checking", IsActive = true, OpeningBalance = 1000, OpeningBalanceDate = new(2026,10,1) },
            new Account { Id = 2, HouseholdId = 1, Name = "Savings", IsActive = true, OpeningBalance = 500, OpeningBalanceDate = new(2026,10,1) },
            new Account { Id = 3, HouseholdId = 2, Name = "Foreign", IsActive = true, OpeningBalanceDate = new(2026,10,1) });
        c.Categories.AddRange(new Category { Id = 1, HouseholdId = 1, Name = "Groceries", CategoryType = CategoryType.Expense, IsActive = true },
            new Category { Id = 2, HouseholdId = 1, Name = "Salary", CategoryType = CategoryType.Income, IsActive = true },
            new Category { Id = 3, HouseholdId = 2, Name = "Foreign", CategoryType = CategoryType.Expense, IsActive = true });
        c.Transactions.AddRange(new Transaction { Id = 1, HouseholdId = 1, AccountId = 1, CategoryId = 1, Amount = 100, TransactionDate = new(2026,10,5), Description = "Groceries", Notes = "Keep note" },
            new Transaction { Id = 2, HouseholdId = 1, AccountId = 1, CategoryId = 2, Amount = 200, TransactionDate = new(2026,10,20), Description = "Payday" });
        c.AccountBalanceSnapshots.AddRange(new() { AccountId = 1, SnapshotDate = new(2026,10,5), Balance = 900 },
            new() { AccountId = 1, SnapshotDate = new(2026,10,20), Balance = 1100 }, new() { AccountId = 2, SnapshotDate = new(2026,10,5), Balance = 500 });
        await c.SaveChangesAsync();
    }
    private static UpdateTransactionRequest Request(TransactionEditDetails details) {
        var t = details.Transaction;
        return new() { ExpectedEditVersion = t.EditVersion, ExpectedPairedEditVersion = details.PairedEditVersion, AccountId = t.AccountId,
            CategoryId = t.CategoryId, CategoryType = t.CategoryType, Amount = t.Amount, Date = DateOnly.FromDateTime(t.TransactionDate),
            Description = t.Description, Merchant = t.Merchant, Notes = t.Notes, IsCleared = t.IsCleared, Direction = t.TransferDirection, Counterparty = t.TransferCounterparty };
    }
    [Fact] public async Task Edits_rebuild_old_new_and_later_snapshots_in_both_accounts()
    {
        await using var db = await BudgetTestDatabase.CreateAsync(); await Seed(db);
        var r = Request(await Service(db).GetAsync(1)); r.Amount = 150; r.Date = new(2026,10,25);
        var saved = await Service(db).UpdateAsync(1, r); Assert.Equal("Keep note", saved.Transaction.Notes); Assert.Equal(1, saved.Transaction.EditVersion);
        var c = db.Context(); var snapshots = await c.AccountBalanceSnapshots.AsNoTracking().Where(s => s.AccountId == 1).OrderBy(s => s.SnapshotDate).ToListAsync();
        Assert.Equal(new[] { 1000m, 1200m, 1050m }, snapshots.Select(s => s.Balance));
        r = Request(saved); r.AccountId = 2; r.Date = new(2026,10,6);
        saved = await Service(db).UpdateAsync(1, r);
        Assert.Equal(1200, await new AccountBalanceService(c).GetCurrentBalanceAsync(1)); Assert.Equal(350, await new AccountBalanceService(c).GetCurrentBalanceAsync(2));
        Assert.All(await c.AccountBalanceSnapshots.AsNoTracking().Where(s => s.AccountId == 1).ToListAsync(), s => Assert.Equal(s.SnapshotDate.Day < 20 ? 1000m : 1200m, s.Balance));
        await Service(db).DeleteAsync(1, saved.Transaction.EditVersion, null);
        Assert.Equal(500, await new AccountBalanceService(c).GetCurrentBalanceAsync(2)); Assert.Single(await c.Transactions.ToListAsync());
    }
    [Fact] public async Task Category_corrections_change_balance_sign_and_validate_ownership_dates_and_precision()
    {
        await using var db = await BudgetTestDatabase.CreateAsync(); await Seed(db);
        var r = Request(await Service(db).GetAsync(1)); r.CategoryType = CategoryType.Income; r.CategoryId = 2;
        var saved = await Service(db).UpdateAsync(1, r); Assert.Equal(1300, await new AccountBalanceService(db.Context()).GetCurrentBalanceAsync(1));
        r = Request(saved); r.CategoryId = 3; await Assert.ThrowsAsync<ArgumentException>(() => Service(db).UpdateAsync(1, r));
        r.CategoryId = 2; r.AccountId = 3; await Assert.ThrowsAsync<ArgumentException>(() => Service(db).UpdateAsync(1, r));
        r.AccountId = 1; r.Date = new(2026,9,30); await Assert.ThrowsAsync<ArgumentException>(() => Service(db).UpdateAsync(1, r));
        r.Date = new(2026,10,5); r.Amount = 1.001m; await Assert.ThrowsAsync<ArgumentException>(() => Service(db).UpdateAsync(1, r));
        await Assert.ThrowsAsync<TransactionNotFoundException>(() => Service(db, 2).GetAsync(1));
        await Assert.ThrowsAsync<TransactionNotFoundException>(() => Service(db, 2).UpdateAsync(1, r));
        await Assert.ThrowsAsync<TransactionNotFoundException>(() => Service(db, 2).DeleteAsync(1, 1, null));
    }
    [Fact] public async Task Stale_saves_and_deletes_cannot_overwrite_newer_changes()
    {
        await using var db = await BudgetTestDatabase.CreateAsync(); await Seed(db);
        var stale = Request(await Service(db).GetAsync(1)); var fresh = Request(await Service(db).GetAsync(1)); fresh.Notes = "Updated";
        await Service(db).UpdateAsync(1, fresh);
        await Assert.ThrowsAsync<TransactionConflictException>(() => Service(db).UpdateAsync(1, stale));
        await Assert.ThrowsAsync<TransactionConflictException>(() => Service(db).DeleteAsync(1, 0, null));
        Assert.Equal("Updated", (await Service(db).GetAsync(1)).Transaction.Notes);
    }
    [Fact] public async Task Linked_transfer_updates_amount_on_both_sides_and_deletes_pair_atomically()
    {
        await using var db = await BudgetTestDatabase.CreateAsync(); await Seed(db);
        var c = db.Context(); var transfers = new TransferService(c, new User(), new AccountBalanceSnapshotService(c, new User()));
        var pair = await transfers.CreateAsync(new() { FromAccountId = 1, ToAccountId = 2, Date = new(2026,10,12), Amount = 100 });
        var r = Request(await Service(db).GetAsync(pair.Outgoing.Id)); r.Amount = 140; r.Date = new(2026,10,10);
        var saved = await Service(db).UpdateAsync(pair.Outgoing.Id, r);
        var other = await Service(db).GetAsync(pair.Incoming.Id);
        Assert.Equal(140, other.Transaction.Amount); Assert.Equal(12, other.Transaction.TransactionDate.Day);
        Assert.Equal(960, await new AccountBalanceService(db.Context()).GetCurrentBalanceAsync(1)); Assert.Equal(640, await new AccountBalanceService(db.Context()).GetCurrentBalanceAsync(2));
        await Assert.ThrowsAsync<TransactionConflictException>(() => Service(db).DeleteAsync(pair.Outgoing.Id, saved.Transaction.EditVersion, r.ExpectedPairedEditVersion));
        r = Request(saved); r.AccountId = 2; await Assert.ThrowsAsync<ArgumentException>(() => Service(db).UpdateAsync(pair.Outgoing.Id, r));
        await Service(db).DeleteAsync(pair.Outgoing.Id, saved.Transaction.EditVersion, saved.PairedEditVersion);
        Assert.Equal(2, await db.Context().Transactions.CountAsync());
        Assert.Equal(1100, await new AccountBalanceService(db.Context()).GetCurrentBalanceAsync(1)); Assert.Equal(500, await new AccountBalanceService(db.Context()).GetCurrentBalanceAsync(2));
    }
    [Fact] public async Task Imported_bill_linked_entry_keeps_provenance_and_independent_paid_status()
    {
        await using var db = await BudgetTestDatabase.CreateAsync(); await Seed(db); var c = db.Context();
        var batch = new TransactionImportBatch { HouseholdId = 1, AccountId = 1, SubmissionId = Guid.NewGuid(), Filename = "bank.csv", PayloadHash = new('a',64), CreatedByUserId = "test", ImportedCount = 1 };
        c.TransactionImportBatches.Add(batch); await c.SaveChangesAsync();
        var entry = await c.Transactions.SingleAsync(t => t.Id == 1); entry.ImportBatchId = batch.Id; entry.ImportRowId = 2;
        var bill = new BillPayment { HouseholdId = 1, AllocationKey = Guid.NewGuid(), DueDate = new(2026,10,5), IsPaid = true, PaidOn = new(2026,10,5), TransactionId = 1 };
        c.BillPayments.Add(bill); await c.SaveChangesAsync();
        var r = Request(await Service(db).GetAsync(1)); r.Notes = "Corrected";
        var saved = await Service(db).UpdateAsync(1, r); Assert.Equal(batch.Id, saved.Transaction.ImportBatchId); Assert.True(saved.Bill!.IsPaid);
        r = Request(saved); r.CategoryType = CategoryType.Income; r.CategoryId = 2;
        await Assert.ThrowsAsync<TransactionConflictException>(() => Service(db).UpdateAsync(1, r));
        await Service(db).DeleteAsync(1, saved.Transaction.EditVersion, null);
        var retained = await db.Context().BillPayments.AsNoTracking().SingleAsync(); Assert.True(retained.IsPaid); Assert.Null(retained.TransactionId);
        Assert.Equal(1, (await db.Context().TransactionImportBatches.SingleAsync()).ImportedCount);
    }
    [Fact] public async Task Snapshot_failure_rolls_back_both_transfer_entries_and_their_versions()
    {
        await using var db = await BudgetTestDatabase.CreateAsync(); await Seed(db); var c = db.Context();
        var transfers = new TransferService(c, new User(), new AccountBalanceSnapshotService(c, new User()));
        var pair = await transfers.CreateAsync(new() { FromAccountId = 1, ToAccountId = 2, Date = new(2026,10,12), Amount = 100 });
        var before = await Service(db).GetAsync(pair.Outgoing.Id);
        await c.Database.ExecuteSqlRawAsync("CREATE TRIGGER FailSnapshot BEFORE UPDATE ON AccountBalanceSnapshots WHEN NEW.AccountId = 2 BEGIN SELECT RAISE(ABORT, 'Test storage failure'); END;");
        var r = Request(before); r.Amount = 140;
        await Assert.ThrowsAsync<DbUpdateException>(() => Service(db).UpdateAsync(pair.Outgoing.Id, r));
        var after = await Service(db).GetAsync(pair.Outgoing.Id);
        Assert.Equal(before.Transaction.EditVersion, after.Transaction.EditVersion); Assert.Equal(before.PairedEditVersion, after.PairedEditVersion);
        Assert.Equal(100, after.Transaction.Amount); Assert.Equal(100, (await Service(db).GetAsync(pair.Incoming.Id)).Transaction.Amount);
        Assert.Equal(1000, await new AccountBalanceService(db.Context()).GetCurrentBalanceAsync(1)); Assert.Equal(600, await new AccountBalanceService(db.Context()).GetCurrentBalanceAsync(2));
    }
    [Fact] public async Task Unlinked_transfers_can_be_corrected_to_expenses_and_back_without_double_entries()
    {
        await using var db = await BudgetTestDatabase.CreateAsync(); await Seed(db);
        var r = Request(await Service(db).GetAsync(1)); r.CategoryType = CategoryType.Transfer; r.Direction = TransferDirection.In; r.Counterparty = TransferCounterparty.External; r.CategoryId = null;
        var saved = await Service(db).UpdateAsync(1, r); Assert.Equal(TransferCounterparty.External, saved.Transaction.TransferCounterparty);
        r = Request(saved); r.CategoryType = CategoryType.Expense; r.CategoryId = 1;
        saved = await Service(db).UpdateAsync(1, r); Assert.Null(saved.Transaction.TransferDirection); Assert.Null(saved.Transaction.TransferCounterparty);
        Assert.Equal(2, await db.Context().Transactions.CountAsync()); Assert.Equal(1100, await new AccountBalanceService(db.Context()).GetCurrentBalanceAsync(1));
    }
}
