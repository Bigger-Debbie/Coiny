using Coiny.API.Data;
using Coiny.API.DTOs.Transactions;
using Coiny.API.Enums;
using Coiny.API.Interfaces;
using Coiny.API.Models;
using Coiny.API.Services;
using Microsoft.EntityFrameworkCore;

namespace Coiny.API.Tests;

public class TransactionImportTests
{
    private static readonly DateOnly Day = new(2026, 10, 1);
    private sealed class User(int householdId = 1) : ICurrentUserService
    {
        public string? GetUserId() => "test-user";
        public bool IsAuthenticated() => true;
        public Task<int> GetCurrentHouseholdIdAsync() => Task.FromResult(householdId);
    }
    private static TransactionImportService Service(CoinyContext c, int householdId = 1) => new(c, new User(householdId),
        new AccountBalanceSnapshotService(c, new User(householdId)), new AccountBalanceService(c), new TestClock());
    private static async Task Seed(CoinyContext c)
    {
        c.Accounts.AddRange(new Account { Id = 1, HouseholdId = 1, Name = "Checking", OpeningBalance = 1000, OpeningBalanceDate = Day },
            new Account { Id = 2, HouseholdId = 2, Name = "Foreign", OpeningBalanceDate = Day });
        c.Categories.AddRange(new Category { Id = 1, HouseholdId = 1, Name = "Groceries", CategoryType = CategoryType.Expense, IsActive = true },
            new Category { Id = 2, HouseholdId = 1, Name = "Salary", CategoryType = CategoryType.Income, IsActive = true },
            new Category { Id = 3, HouseholdId = 2, Name = "Foreign category", CategoryType = CategoryType.Expense, IsActive = true });
        c.AccountBalanceSnapshots.Add(new() { AccountId = 1, SnapshotDate = Day.AddDays(5), Balance = 1000 });
        await c.SaveChangesAsync();
    }
    private static TransactionImportRow Row(int id = 2) => new() { RowId = id, Date = Day.AddDays(1), Amount = 20,
        Direction = TransferDirection.Out, CategoryType = CategoryType.Expense, CategoryId = 1, Description = "Weekly groceries" };
    private static TransactionImportRequest Request(params TransactionImportRow[] rows) => new()
    { AccountId = 1, SubmissionId = Guid.NewGuid(), Filename = "bank.csv", Rows = rows.Length > 0 ? rows.ToList() : [Row()] };

    [Fact]
    public async Task Separately_imported_bills_and_spending_transfers_reconcile_without_changing_balances_or_import_history()
    {
        await using var db = await BudgetTestDatabase.CreateAsync(); var c = db.Context(); await Seed(c);
        c.Accounts.Add(new Account { Id = 3, HouseholdId = 1, Name = "Spending", OpeningBalance = 100, OpeningBalanceDate = Day }); await c.SaveChangesAsync();
        var outgoing = Request(new TransactionImportRow { RowId = 2, Date = Day.AddDays(1), Amount = 250, CategoryType = CategoryType.Transfer, Direction = TransferDirection.Out, Description = "Transfer to Spending" });
        var incoming = new TransactionImportRequest { AccountId = 3, SubmissionId = Guid.NewGuid(), Filename = "spending.csv", Rows = [new() {
            RowId = 2, Date = Day.AddDays(2), Amount = 250, CategoryType = CategoryType.Transfer, Direction = TransferDirection.In, Description = "Transfer from Bills" }] };
        var billsImport = await Service(db.Context()).ImportAsync(outgoing); var spendingImport = await Service(db.Context()).ImportAsync(incoming);
        using var ledger = db.Context(); var rows = await ledger.Transactions.AsNoTracking().OrderBy(t => t.Id).ToListAsync();
        var snapshots = await ledger.AccountBalanceSnapshots.AsNoTracking().OrderBy(s => s.Id).Select(s => new { s.AccountId, s.SnapshotDate, s.Balance }).ToArrayAsync();
        var transfers = new TransferService(ledger, new User(), new AccountBalanceSnapshotService(ledger,new User()));
        var matches = await transfers.GetCandidatesAsync(rows[0].Id, 3, 7); Assert.Single(matches); Assert.Equal(rows[1].Id, matches[0].Id);
        var pair = await transfers.LinkAsync(new() { FirstTransactionId = rows[0].Id, SecondTransactionId = rows[1].Id, ExpectedFirstEditVersion = rows[0].EditVersion, ExpectedSecondEditVersion = rows[1].EditVersion });
        Assert.Equal(billsImport.BatchId, pair.Outgoing.ImportBatchId); Assert.Equal(spendingImport.BatchId, pair.Incoming.ImportBatchId);
        Assert.Equal(rows[0].TransactionDate, pair.Outgoing.TransactionDate); Assert.Equal(rows[1].TransactionDate, pair.Incoming.TransactionDate);
        Assert.Equal(750, await new AccountBalanceService(ledger).GetCurrentBalanceAsync(1)); Assert.Equal(350, await new AccountBalanceService(ledger).GetCurrentBalanceAsync(3));
        Assert.Equal(snapshots, await ledger.AccountBalanceSnapshots.AsNoTracking().OrderBy(s => s.Id).Select(s => new { s.AccountId, s.SnapshotDate, s.Balance }).ToArrayAsync());
        Assert.Equal(2, await ledger.Transactions.CountAsync()); Assert.Equal(2, await ledger.TransactionImportBatches.CountAsync());
        Assert.Equal(billsImport, await Service(db.Context()).ImportAsync(outgoing)); Assert.Equal(spendingImport, await Service(db.Context()).ImportAsync(incoming));
        Assert.Equal(2, await ledger.Transactions.CountAsync());
        await transfers.UnlinkAsync(rows[0].Id, pair.Outgoing.EditVersion, pair.Incoming.EditVersion);
        Assert.Equal(750, await new AccountBalanceService(ledger).GetCurrentBalanceAsync(1)); Assert.Equal(350, await new AccountBalanceService(ledger).GetCurrentBalanceAsync(3));
        Assert.Equal(2, await ledger.Transactions.CountAsync());
    }

    [Fact]
    public async Task Bank_comparison_uses_existing_history_and_included_rows_through_its_date_only()
    {
        await using var db = await BudgetTestDatabase.CreateAsync(); var c = db.Context(); await Seed(c);
        c.Transactions.AddRange(new Transaction { HouseholdId = 1, AccountId = 1, CategoryId = 1, Amount = 25, TransactionDate = Day.ToDateTime(TimeOnly.MinValue), Description = "Already imported" },
            new Transaction { HouseholdId = 1, AccountId = 1, CategoryId = 1, Amount = 40, TransactionDate = Day.AddDays(10).ToDateTime(TimeOnly.MinValue), Description = "Later spending" });
        await c.SaveChangesAsync();
        var request = Request(); request.BankBalanceCheck = new() { Date = Day.AddDays(1), Balance = 955 };
        var validation = await Service(c).ValidateAsync(request);
        Assert.True(validation.CanImport); Assert.Equal(915, validation.ProjectedBalance);
        Assert.Equal(955, validation.BalanceComparison!.CalculatedBalance); Assert.Equal(0, validation.BalanceComparison.Difference);
        Assert.False(validation.BalanceComparison.RequiresAcknowledgment);
        Assert.Equal(1000, (await c.Accounts.SingleAsync(a => a.Id == 1)).OpeningBalance);
    }

    [Fact]
    public async Task Balance_mismatch_requires_exact_acknowledgment_without_creating_adjustments()
    {
        await using var db = await BudgetTestDatabase.CreateAsync(); var c = db.Context(); await Seed(c);
        var request = Request(); request.BankBalanceCheck = new() { Date = Day.AddDays(1), Balance = 990 };
        var validation = await Service(c).ValidateAsync(request);
        Assert.False(validation.CanImport); Assert.Equal(10, validation.BalanceComparison!.Difference);
        await Assert.ThrowsAsync<ImportValidationException>(() => Service(c).ImportAsync(request));
        Assert.Empty(await c.Transactions.ToListAsync()); Assert.Empty(await c.TransactionImportBatches.ToListAsync());
        request.BankBalanceCheck.AcknowledgedCalculatedBalance = 980;
        Assert.True((await Service(c).ValidateAsync(request)).CanImport);
        var result = await Service(c).ImportAsync(request); Assert.Equal(980, result.BalanceAfter);
        Assert.Single(await c.Transactions.ToListAsync()); Assert.Equal(1000, (await c.Accounts.SingleAsync(a => a.Id == 1)).OpeningBalance);
        Assert.Equal(result, await Service(db.Context()).ImportAsync(request));
        request.BankBalanceCheck.Balance = 991;
        await Assert.ThrowsAsync<ImportConflictException>(() => Service(db.Context()).ImportAsync(request));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Commit_rechecks_balances_and_rejects_new_or_changed_differences(bool initiallyMatches)
    {
        await using var db = await BudgetTestDatabase.CreateAsync(); var c = db.Context(); await Seed(c);
        var request = Request(); request.BankBalanceCheck = new() { Date = Day.AddDays(1), Balance = initiallyMatches ? 980 : 990 };
        var preview = await Service(c).ValidateAsync(request);
        request.BankBalanceCheck.AcknowledgedCalculatedBalance = initiallyMatches ? null : preview.BalanceComparison!.CalculatedBalance;
        c.Transactions.Add(new() { HouseholdId = 1, AccountId = 1, CategoryId = 1, Amount = 1, TransactionDate = Day.ToDateTime(TimeOnly.MinValue), Description = "Concurrent spending" }); await c.SaveChangesAsync();
        var error = await Assert.ThrowsAsync<ImportValidationException>(() => Service(db.Context()).ImportAsync(request));
        Assert.Equal(979, error.Validation.BalanceComparison!.CalculatedBalance); Assert.False(error.Validation.CanImport);
        Assert.Empty(await c.TransactionImportBatches.ToListAsync());
        request.BankBalanceCheck.AcknowledgedCalculatedBalance = 979;
        Assert.Equal(979, (await Service(db.Context()).ImportAsync(request)).BalanceAfter);
    }

    [Fact]
    public async Task Bank_reference_rejects_unsupported_accounts_invalid_precision_and_dates()
    {
        await using var db = await BudgetTestDatabase.CreateAsync(); var c = db.Context(); await Seed(c);
        var request = Request(); request.BankBalanceCheck = new() { Date = Day, Balance = 1000 };
        await Assert.ThrowsAsync<ArgumentException>(() => Service(c).ValidateAsync(request));
        request.BankBalanceCheck.Date = Day.AddDays(1); request.BankBalanceCheck.Balance = 1.001m;
        await Assert.ThrowsAsync<ArgumentException>(() => Service(c).ValidateAsync(request));
        request.BankBalanceCheck.Balance = -20;
        Assert.Equal(-20, (await Service(c).ValidateAsync(request)).BalanceComparison!.BankBalance);
        var account = await c.Accounts.SingleAsync(a => a.Id == 1); account.AccountType = AccountType.CreditCard; await c.SaveChangesAsync();
        await Assert.ThrowsAsync<ArgumentException>(() => Service(c).ValidateAsync(request));
    }

    [Fact]
    public async Task Batch_updates_all_dates_once_and_preserves_later_history_and_transfer_exclusion()
    {
        await using var db = await BudgetTestDatabase.CreateAsync(); var c = db.Context(); await Seed(c);
        var income = Row(3); income.Date = Day.AddDays(2); income.Direction = TransferDirection.In; income.CategoryType = CategoryType.Income; income.CategoryId = 2; income.Amount = 100;
        var transfer = Row(4); transfer.Date = Day.AddDays(2); transfer.CategoryType = CategoryType.Transfer; transfer.CategoryId = null; transfer.Amount = 10;
        var request = Request(Row(), income, transfer);
        var validation = await Service(c).ValidateAsync(request);
        Assert.True(validation.CanImport); Assert.Equal(1070, validation.ProjectedBalance);
        Assert.Empty(await c.Transactions.ToListAsync());
        var result = await Service(c).ImportAsync(request);
        Assert.Equal(3, result.ImportedCount); Assert.Equal(1070, result.BalanceAfter);
        Assert.Equal(1070, await new AccountBalanceService(c).GetCurrentBalanceAsync(1));
        var snapshots = await c.AccountBalanceSnapshots.OrderBy(s => s.SnapshotDate).ToListAsync();
        Assert.Equal(new[] { 980m, 1070m, 1070m }, snapshots.Select(s => s.Balance));
        Assert.All(await c.Transactions.ToListAsync(), t => { Assert.Equal(result.BatchId, t.ImportBatchId); Assert.NotNull(t.ImportRowId); });
        var entry = await c.Transactions.SingleAsync(t => t.ImportRowId == 4);
        Assert.Null(entry.PairedTransactionId); Assert.Equal(TransferCounterparty.Unknown, entry.TransferCounterparty);
        var transactions = new TransactionService(c, new User(), new AccountBalanceSnapshotService(c, new User()));
        Assert.Equal(20, (await transactions.GetCategoryBreakdownAsync(new() { StartDate = Day, EndDate = Day.AddDays(5), CategoryType = CategoryType.Expense })).TotalAmount);
    }

    [Fact]
    public async Task Repeat_submission_is_idempotent_and_conflicting_reuse_is_rejected()
    {
        await using var db = await BudgetTestDatabase.CreateAsync(); var c = db.Context(); await Seed(c);
        var request = Request(); var first = await Service(c).ImportAsync(request);
        request.Rows[0].Amount = 20.00m;
        var replay = await Service(db.Context()).ImportAsync(request);
        Assert.Equal(first, replay); Assert.Single(await c.Transactions.ToListAsync());
        Assert.Single(await c.TransactionImportBatches.ToListAsync());
        request.Rows[0].Amount = 21;
        await Assert.ThrowsAsync<ImportConflictException>(() => Service(db.Context()).ImportAsync(request));
    }

    [Fact]
    public async Task Duplicates_in_file_and_ledger_require_explicit_decisions()
    {
        await using var db = await BudgetTestDatabase.CreateAsync(); var c = db.Context(); await Seed(c);
        var request = Request(Row(2), Row(3));
        var duplicate = await Service(c).ValidateAsync(request);
        Assert.False(duplicate.CanImport); Assert.All(duplicate.Rows, row => Assert.Single(row.DuplicateRowIds));
        await Assert.ThrowsAsync<ImportValidationException>(() => Service(c).ImportAsync(request));
        Assert.Empty(await c.TransactionImportBatches.ToListAsync());
        request.Rows.RemoveAt(1); await Service(c).ImportAsync(request);
        var overlap = Request(); overlap.Rows[0].Description = "  WEEKLY   groceries ";
        var ledger = await Service(c).ValidateAsync(overlap);
        Assert.False(ledger.CanImport); Assert.Single(ledger.Rows[0].DuplicateTransactionIds);
        overlap.Rows[0].AllowDuplicate = true;
        await Service(c).ImportAsync(overlap);
        Assert.Equal(2, await c.Transactions.CountAsync());
    }

    [Fact]
    public async Task Invalid_and_foreign_rows_cannot_write_any_part_of_a_batch()
    {
        await using var db = await BudgetTestDatabase.CreateAsync(); var c = db.Context(); await Seed(c);
        var invalid = Row(); invalid.Date = Day.AddDays(-1); invalid.Amount = 1.234m; invalid.CategoryId = 3;
        var request = Request(invalid);
        var validation = await Service(c).ValidateAsync(request);
        Assert.True(validation.Rows[0].Errors.Count >= 3);
        await Assert.ThrowsAsync<ImportValidationException>(() => Service(c).ImportAsync(request));
        request.AccountId = 2;
        await Assert.ThrowsAsync<ArgumentException>(() => Service(c).ValidateAsync(request));
        Assert.Empty(await c.Transactions.ToListAsync());
        Assert.Empty(await c.TransactionImportBatches.ToListAsync());
    }

    [Fact]
    public async Task Commit_rechecks_new_duplicates_after_preview()
    {
        await using var db = await BudgetTestDatabase.CreateAsync(); var c = db.Context(); await Seed(c);
        var request = Request(); Assert.True((await Service(c).ValidateAsync(request)).CanImport);
        await Service(db.Context()).ImportAsync(Request());
        await Assert.ThrowsAsync<ImportValidationException>(() => Service(c).ImportAsync(request));
        Assert.Single(await c.TransactionImportBatches.ToListAsync());
    }

    [Fact]
    public async Task Snapshot_failure_rolls_back_batch_transactions_and_new_transfer_category()
    {
        await using var db = await BudgetTestDatabase.CreateAsync(); var c = db.Context(); await Seed(c);
        await c.Database.ExecuteSqlRawAsync("CREATE TRIGGER fail_import_snapshot BEFORE INSERT ON AccountBalanceSnapshots BEGIN SELECT RAISE(ABORT, 'test failure'); END;");
        var transfer = Row(); transfer.CategoryType = CategoryType.Transfer; transfer.CategoryId = null;
        await Assert.ThrowsAsync<DbUpdateException>(() => Service(c).ImportAsync(Request(transfer)));
        var verify = db.Context();
        Assert.Empty(await verify.Transactions.ToListAsync()); Assert.Empty(await verify.TransactionImportBatches.ToListAsync());
        Assert.False(await verify.Categories.AnyAsync(cat => cat.CategoryType == CategoryType.Transfer));
        Assert.Equal(1000, (await verify.AccountBalanceSnapshots.SingleAsync()).Balance);
    }

    [Fact]
    public async Task History_is_household_scoped_and_row_limits_are_enforced()
    {
        await using var db = await BudgetTestDatabase.CreateAsync(); var c = db.Context(); await Seed(c);
        await Service(c).ImportAsync(Request());
        Assert.Single(await Service(c).HistoryAsync(1)); Assert.Empty(await Service(c, 2).HistoryAsync(null));
        await Assert.ThrowsAsync<ArgumentException>(() => Service(c, 2).HistoryAsync(1));
        var large = Request(); large.Rows = Enumerable.Range(1, 2001).Select(Row).ToList();
        await Assert.ThrowsAsync<ArgumentException>(() => Service(c).ValidateAsync(large));
        var direction = Row(); direction.Direction = TransferDirection.In;
        Assert.False((await Service(c).ValidateAsync(Request(direction))).CanImport);
    }
    [Fact]
    public async Task Concurrent_retries_create_one_batch_and_one_transaction()
    {
        await using var db = await InvitationTestDatabase.Create(); await Seed(db.Context());
        var request = Request();
        var results = await Task.WhenAll(Enumerable.Range(0, 2).Select(_ => Task.Run(() => Service(db.Context()).ImportAsync(request))));
        Assert.Equal(results[0].BatchId, results[1].BatchId);
        var verify = db.Context(); Assert.Single(await verify.Transactions.ToListAsync()); Assert.Single(await verify.TransactionImportBatches.ToListAsync());
    }
    [Fact]
    public async Task Full_size_batch_creates_a_daily_snapshot_for_every_imported_date()
    {
        await using var db = await BudgetTestDatabase.CreateAsync(); var c = db.Context(); await Seed(c);
        var request = Request(); request.Rows = Enumerable.Range(1, 2000).Select(id =>
        { var row = Row(id); row.Description = $"Transaction {id}"; row.Amount = .01m; row.Date = Day.AddDays(id % 15); return row; }).ToList();
        var imported = await Service(c).ImportAsync(request);
        Assert.Equal(2000, imported.ImportedCount); Assert.Equal(980, imported.BalanceAfter);
        Assert.Equal(15, await c.AccountBalanceSnapshots.CountAsync());
        Assert.Equal(980, await new AccountBalanceService(c).GetCurrentBalanceAsync(1));
    }
}
