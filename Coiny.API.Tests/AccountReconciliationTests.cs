using Coiny.API.Data;
using Coiny.API.DTOs.Accounts;
using Coiny.API.Enums;
using Coiny.API.Interfaces;
using Coiny.API.Models;
using Coiny.API.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Coiny.API.Tests;

public class AccountReconciliationTests
{
    private sealed class User(int household = 1, string id = "test") : ICurrentUserService
    {
        public string? GetUserId() => id;
        public bool IsAuthenticated() => true;
        public Task<int> GetCurrentHouseholdIdAsync() => Task.FromResult(household);
    }
    private static AccountReconciliationService Service(BudgetTestDatabase db, int household = 1, string user = "test") => new(db.Context(), new User(household, user), db.Clock);
    private static async Task Seed(BudgetTestDatabase db)
    {
        var c = db.Context();
        c.Users.Add(new() { Id = "test", FirstName = "Alex", Email = "alex@example.test" });
        c.Accounts.AddRange(new Account { Id = 1, HouseholdId = 1, Name = "Checking", AccountType = AccountType.Checking, OpeningBalance = 1000, OpeningBalanceDate = new(2026, 10, 1) },
            new Account { Id = 2, HouseholdId = 1, Name = "Credit", AccountType = AccountType.CreditCard, OpeningBalance = 1000, OpeningBalanceDate = new(2026, 10, 1) },
            new Account { Id = 3, HouseholdId = 2, Name = "Foreign", OpeningBalanceDate = new(2026, 10, 1) });
        c.Categories.AddRange(new Category { Id = 1, HouseholdId = 1, Name = "Groceries", CategoryType = CategoryType.Expense, IsActive = true },
            new Category { Id = 2, HouseholdId = 1, Name = "Pay", CategoryType = CategoryType.Income, IsActive = true },
            new Category { Id = 3, HouseholdId = 1, Name = "Transfer", CategoryType = CategoryType.Transfer, IsSystem = true, IsActive = true });
        c.Transactions.AddRange(new Transaction { Id = 1, HouseholdId = 1, AccountId = 1, CategoryId = 1, Amount = 100, TransactionDate = new(2026, 10, 5, 23, 59, 59), Description = "Groceries" },
            new Transaction { Id = 2, HouseholdId = 1, AccountId = 1, CategoryId = 2, Amount = 200, TransactionDate = new(2026, 10, 6), Description = "Payday" },
            new Transaction { Id = 3, HouseholdId = 1, AccountId = 2, CategoryId = 1, Amount = 100, TransactionDate = new(2026, 10, 5), Description = "Purchase" });
        await c.SaveChangesAsync();
    }
    private static ReconciliationCheckRequest Request(decimal balance = 900, int day = 5) => new() { StatementDate = new(2026, 10, day), StatementBalance = balance };
    private static async Task<AccountReconciliationResponse> Confirm(BudgetTestDatabase db, int account = 1, decimal balance = 900, int day = 5)
    {
        var check = await Service(db).CheckAsync(account, Request(balance, day));
        return await Service(db).ConfirmAsync(account, Confirmation(check));
    }
    private static ConfirmReconciliationRequest Confirmation(ReconciliationCheck check) => new() { StatementDate = check.StatementDate, StatementBalance = check.StatementBalance, ReviewedFingerprint = check.LedgerFingerprint, TransactionsReviewed = true };

    [Fact]
    public async Task Household_upgrade_preserves_existing_checkpoint_and_adds_nullable_audit_fields()
    {
        await using var c=new Coiny.API.Data.Hybrid.HouseholdContext(new DbContextOptionsBuilder<Coiny.API.Data.Hybrid.HouseholdContext>().UseSqlite("Data Source=:memory:").Options);
        await c.Database.OpenConnectionAsync();
        var priorMigration = c.Database.GetMigrations().TakeWhile(m => !m.EndsWith("_ReconciliationAdjustments")).Last();
        await c.GetService<IMigrator>().MigrateAsync(priorMigration);
        c.Accounts.Add(new Account{Id=1,HouseholdId=1,Name="Checking",OpeningBalance=100,OpeningBalanceDate=new(2026,10,1)});await c.SaveChangesAsync();
        await c.Database.ExecuteSqlRawAsync("INSERT INTO AccountReconciliations (Id,HouseholdId,AccountId,StatementDate,StatementBalance,LedgerFingerprint,ConfirmedByUserId,ConfirmedByName,CreatedAt) VALUES (1,1,1,'2026-10-05','100','original-fingerprint','test','Reviewer','2026-10-06 12:00:00')");
        await c.Database.MigrateAsync();
        var record=await c.AccountReconciliations.AsNoTracking().SingleAsync();
        Assert.Equal(100,record.StatementBalance);Assert.Equal("original-fingerprint",record.LedgerFingerprint);
        Assert.Null(record.AdjustmentOperationId);Assert.Null(record.AdjustmentAmount);Assert.Null(record.AdjustmentTransactionId);
        Assert.Equal(100,(await c.Accounts.SingleAsync()).OpeningBalance);
    }

    private static AdjustReconciliationRequest Adjustment(ReconciliationCheck check) => new() {OperationId=Guid.NewGuid(),StatementDate=check.StatementDate,StatementBalance=check.StatementBalance,ReviewedFingerprint=check.LedgerFingerprint,TransactionsReviewed=true,AdjustmentAcknowledged=true,Reason="Statement reviewed; unresolved legacy difference"};
    [Theory]
    [InlineData(1,950,50,1150)]
    [InlineData(1,850,-50,1050)]
    [InlineData(2,1150,-50,1150)]
    [InlineData(2,1050,50,1050)]
    public async Task Adjustment_reconciles_assets_and_liabilities_without_changing_opening_or_later_transactions(int account,decimal target,decimal stored,decimal current)
    {
        await using var db=await BudgetTestDatabase.CreateAsync();await Seed(db);
        var check=await Service(db).CheckAsync(account,Request(target));var request=Adjustment(check);
        var result=await Service(db).AdjustAsync(account,request);
        using var c=db.Context();var entry=await c.Transactions.Include(t=>t.Category).SingleAsync(t=>t.Id==result.AdjustmentTransactionId);
        Assert.Equal(stored,entry.Amount);Assert.Equal(CategoryType.BalanceAdjustment,entry.Category.CategoryType);
        Assert.Equal(new DateTime(2026,10,5),entry.TransactionDate);Assert.True(entry.IsCleared);
        Assert.Equal(1000,(await c.Accounts.SingleAsync(a=>a.Id==account)).OpeningBalance);
        Assert.Equal(current,await new AccountBalanceService(c).GetCurrentBalanceAsync(account));
        Assert.True((await Service(db).CheckAsync(account,Request(target))).Matches);
        Assert.False(result.NeedsReview);Assert.Equal(check.Difference,result.AdjustmentAmount);
        Assert.Equal(3,await c.Transactions.CountAsync(t=>t.Category.CategoryType!=CategoryType.BalanceAdjustment));
        Assert.Equal(result,await Service(db).AdjustAsync(account,request));
        Assert.Single(await c.Transactions.Where(t=>t.Category.CategoryType==CategoryType.BalanceAdjustment).ToListAsync());
        request.Reason="Changed request";await Assert.ThrowsAsync<ReconciliationConflictException>(()=>Service(db).AdjustAsync(account,request));
    }
    [Fact]
    public async Task Adjustment_requires_explicit_review_and_rejects_changed_ledger_or_target()
    {
        await using var db=await BudgetTestDatabase.CreateAsync();await Seed(db);
        var check=await Service(db).CheckAsync(1,Request(950));var request=Adjustment(check);
        request.AdjustmentAcknowledged=false;await Assert.ThrowsAsync<ArgumentException>(()=>Service(db).AdjustAsync(1,request));request.AdjustmentAcknowledged=true;
        request.TransactionsReviewed=false;await Assert.ThrowsAsync<ArgumentException>(()=>Service(db).AdjustAsync(1,request));request.TransactionsReviewed=true;
        request.StatementBalance=960;await Assert.ThrowsAsync<ReconciliationConflictException>(()=>Service(db).AdjustAsync(1,request));request.StatementBalance=950;
        using var c=db.Context();(await c.Transactions.SingleAsync(t=>t.Id==1)).Amount=101;await c.SaveChangesAsync();
        await Assert.ThrowsAsync<ReconciliationConflictException>(()=>Service(db).AdjustAsync(1,request));
        Assert.Equal(3,await c.Transactions.CountAsync());Assert.Empty(await c.AccountReconciliations.ToListAsync());
        await Assert.ThrowsAsync<ReconciliationNotFoundException>(()=>Service(db,2).AdjustAsync(1,request));
    }
    [Fact]
    public async Task Adjustment_invalidates_old_checkpoints_and_removal_retains_its_audit()
    {
        await using var db=await BudgetTestDatabase.CreateAsync();await Seed(db);var old=await Confirm(db);
        var result=await Service(db).AdjustAsync(1,Adjustment(await Service(db).CheckAsync(1,Request(950))));
        using var c=db.Context();Assert.NotNull((await c.AccountReconciliations.SingleAsync(r=>r.Id==old.Id)).InvalidatedAt);
        var management=new TransactionManagementService(c,new User(),new AccountBalanceSnapshotService(c,new User()));
        await Assert.ThrowsAsync<ArgumentException>(()=>management.UpdateAsync(result.AdjustmentTransactionId!.Value,new(){CategoryType=CategoryType.BalanceAdjustment}));
        await management.DeleteAsync(result.AdjustmentTransactionId!.Value,0,null);
        using var verify=db.Context();var audit=await verify.AccountReconciliations.SingleAsync(r=>r.Id==result.Id);
        Assert.Null(audit.AdjustmentTransactionId);Assert.NotNull(audit.InvalidatedAt);Assert.Equal(50,audit.AdjustmentAmount);Assert.NotNull(audit.AdjustmentReason);
        Assert.Equal(1100,await new AccountBalanceService(verify).GetCurrentBalanceAsync(1));
    }

    [Fact]
    public async Task Compares_the_whole_statement_date_excludes_later_entries_and_uses_liability_signs()
    {
        await using var db = await BudgetTestDatabase.CreateAsync(); await Seed(db);
        var check = await Service(db).CheckAsync(1, Request());
        Assert.True(check.Matches); Assert.Equal(900, check.CalculatedBalance); Assert.Equal(1, check.TransactionCount);
        Assert.True((await Service(db).CheckAsync(2, Request(1100))).Matches);
        Assert.True((await Service(db).CheckAsync(1, Request(1000, 1))).Matches);
        var mismatch = await Service(db).CheckAsync(1, Request(950)); Assert.Equal(50, mismatch.Difference); Assert.False(mismatch.Matches);
        await Assert.ThrowsAsync<ArgumentException>(() => Service(db).ConfirmAsync(1, Confirmation(mismatch)));
        Assert.Empty(await db.Context().AccountReconciliations.ToListAsync());
    }
    [Fact]
    public async Task Confirmation_requires_explicit_review_is_shared_and_idempotent_and_changes_no_ledger_data()
    {
        await using var db = await BudgetTestDatabase.CreateAsync(); await Seed(db);
        var check = await Service(db).CheckAsync(1, Request()); var request = Confirmation(check); request.TransactionsReviewed = false;
        await Assert.ThrowsAsync<ArgumentException>(() => Service(db).ConfirmAsync(1, request));
        request.TransactionsReviewed = true;
        var first = await Service(db).ConfirmAsync(1, request); var retry = await Service(db).ConfirmAsync(1, request);
        Assert.Equal(first.Id, retry.Id); Assert.Equal("Alex", first.ConfirmedByName); Assert.Equal(db.Clock.Now.UtcDateTime, first.ConfirmedAt); Assert.False(first.NeedsReview);
        Assert.Single(await Service(db, user: "other-household-member").HistoryAsync(1));
        Assert.Equal(3, await db.Context().Transactions.CountAsync()); Assert.All(await db.Context().Transactions.ToListAsync(), t => Assert.False(t.IsCleared));
    }
    [Fact]
    public async Task Rejects_foreign_accounts_invalid_dates_precision_and_stale_previews()
    {
        await using var db = await BudgetTestDatabase.CreateAsync(); await Seed(db);
        await Assert.ThrowsAsync<ReconciliationNotFoundException>(() => Service(db).HistoryAsync(3));
        await Assert.ThrowsAsync<ReconciliationNotFoundException>(() => Service(db, 2).CheckAsync(1, Request()));
        await Assert.ThrowsAsync<ArgumentException>(() => Service(db).CheckAsync(1, new() { StatementDate = new(2026, 9, 30) }));
        await Assert.ThrowsAsync<ArgumentException>(() => Service(db).CheckAsync(1, Request(900, 17)));
        await Assert.ThrowsAsync<ArgumentException>(() => Service(db).CheckAsync(1, Request(900.001m)));
        var check = await Service(db).CheckAsync(1, Request());
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Service(db, user: "missing").ConfirmAsync(1, Confirmation(check)));
        var c = db.Context(); (await c.Transactions.SingleAsync(t => t.Id == 1)).Notes = "Corrected statement detail"; await c.SaveChangesAsync();
        await Assert.ThrowsAsync<ReconciliationConflictException>(() => Service(db).ConfirmAsync(1, Confirmation(check)));
        Assert.Empty(await Service(db).HistoryAsync(1));
    }
    [Theory]
    [InlineData("add")]
    [InlineData("delete")]
    [InlineData("amount")]
    [InlineData("date")]
    [InlineData("account")]
    [InlineData("description")]
    [InlineData("cleared")]
    [InlineData("openingBalance")]
    [InlineData("openingDate")]
    public async Task Backdated_changes_permanently_flag_checkpoint_and_allow_a_new_confirmation(string change)
    {
        await using var db = await BudgetTestDatabase.CreateAsync(); await Seed(db); var before = await Confirm(db);
        var c = db.Context(); var entry = await c.Transactions.SingleAsync(t => t.Id == 1);
        switch (change)
        {
            case "add": c.Transactions.Add(new() { HouseholdId = 1, AccountId = 1, CategoryId = 1, Amount = 5, TransactionDate = new(2026, 10, 4), Description = "Imported later" }); break;
            case "delete": c.Remove(entry); break;
            case "amount": entry.Amount = 105; break;
            case "date": entry.TransactionDate = new(2026, 10, 6); break;
            case "account": entry.AccountId = 2; break;
            case "description": entry.Description = "Corrected"; break;
            case "cleared": entry.IsCleared = true; break;
            case "openingBalance": (await c.Accounts.SingleAsync(a => a.Id == 1)).OpeningBalance = 1100; break;
            case "openingDate": (await c.Accounts.SingleAsync(a => a.Id == 1)).OpeningBalanceDate = new(2026, 9, 30); break;
        }
        await c.SaveChangesAsync();
        var history = await Service(db).HistoryAsync(1); var original = Assert.Single(history); Assert.True(original.NeedsReview); Assert.NotNull(original.InvalidatedAt);
        var current = await Service(db).CheckAsync(1, Request());
        await Confirm(db, balance: current.CalculatedBalance);
        history = await Service(db).HistoryAsync(1); Assert.Equal(2, history.Count); Assert.False(history[0].NeedsReview); Assert.Equal(before.Id, history[1].Id);
    }
    [Fact]
    public async Task Later_activity_and_link_metadata_keep_checkpoint_valid_but_moving_later_activity_back_invalidates_it()
    {
        await using var db = await BudgetTestDatabase.CreateAsync(); await Seed(db);
        var setup = db.Context(); var transfer = await setup.Transactions.SingleAsync(t => t.Id == 1);
        transfer.CategoryId = 3; transfer.TransferDirection = TransferDirection.Out; transfer.TransferCounterparty = TransferCounterparty.Unknown;
        await setup.SaveChangesAsync(); await Confirm(db);
        var c = db.Context(); var entry = await c.Transactions.SingleAsync(t => t.Id == 2); entry.Amount = 300;
        var original = await c.Transactions.SingleAsync(t => t.Id == 1); original.EditVersion++; original.TransferCounterparty = TransferCounterparty.External;
        await c.SaveChangesAsync(); Assert.False(Assert.Single(await Service(db).HistoryAsync(1)).NeedsReview);
        entry.TransactionDate = new(2026, 10, 4); await c.SaveChangesAsync(); Assert.True(Assert.Single(await Service(db).HistoryAsync(1)).NeedsReview);
    }
    [Fact]
    public async Task Reverting_an_edit_retains_review_history_and_can_reverify_the_identical_ledger()
    {
        await using var db = await BudgetTestDatabase.CreateAsync(); await Seed(db); await Confirm(db);
        var c = db.Context(); var entry = await c.Transactions.SingleAsync(t => t.Id == 1); entry.Amount = 110; await c.SaveChangesAsync(); entry.Amount = 100; await c.SaveChangesAsync();
        await Confirm(db); var history = await Service(db).HistoryAsync(1); Assert.Equal(2, history.Count); Assert.False(history[0].NeedsReview); Assert.True(history[1].NeedsReview);
    }
    [Fact]
    public async Task Failed_ledger_save_rolls_back_checkpoint_invalidation()
    {
        await using var db = await BudgetTestDatabase.CreateAsync(); await Seed(db); await Confirm(db);
        var c = db.Context();
        await c.Database.ExecuteSqlRawAsync("CREATE TRIGGER FailEdit BEFORE UPDATE ON Transactions BEGIN SELECT RAISE(ABORT, 'Test failure'); END;");
        (await c.Transactions.SingleAsync(t => t.Id == 1)).Amount = 110;
        await Assert.ThrowsAsync<DbUpdateException>(() => c.SaveChangesAsync());
        Assert.False(Assert.Single(await Service(db).HistoryAsync(1)).NeedsReview);
        Assert.Equal(100, (await db.Context().Transactions.SingleAsync(t => t.Id == 1)).Amount);
    }
    [Fact]
    public async Task Account_overview_exposes_latest_checkpoint_and_opening_balance_edits_flag_it()
    {
        await using var db = await BudgetTestDatabase.CreateAsync(); await Seed(db); await Confirm(db);
        var c = db.Context(); var user = new User();
        var accounts = new AccountService(c, user, new AccountBalanceService(c), new AccountBalanceSnapshotService(c, user));
        var account = Assert.Single(await accounts.GetAccountsAsync(), a => a.Id == 1);
        Assert.NotNull(account.LatestReconciliation); Assert.False(account.LatestReconciliation.NeedsReview);
        var updated = await accounts.UpdateAccountAsync(1, new() { Name = "Checking corrected", AccountType = AccountType.Checking, OpeningBalance = 1001, OpeningBalanceDate = new(2026, 10, 1) });
        Assert.True(updated.LatestReconciliation!.NeedsReview); Assert.Equal(900, updated.LatestReconciliation.StatementBalance);
    }
    [Fact]
    public async Task Linking_existing_transfers_keeps_both_checkpoints_valid_and_amount_edit_flags_both()
    {
        await using var db = await BudgetTestDatabase.CreateAsync(); await Seed(db);
        var c = db.Context(); var outEntry = await c.Transactions.SingleAsync(t => t.Id == 1);
        outEntry.CategoryId = 3; outEntry.TransferDirection = TransferDirection.Out; outEntry.TransferCounterparty = TransferCounterparty.Unknown;
        var inEntry = await c.Transactions.SingleAsync(t => t.Id == 3);
        inEntry.CategoryId = 3; inEntry.TransferDirection = TransferDirection.In; inEntry.TransferCounterparty = TransferCounterparty.Unknown;
        await c.SaveChangesAsync(); await Confirm(db); await Confirm(db, account: 2, balance: 900);
        var user = new User(); var transfers = new TransferService(c, user, new AccountBalanceSnapshotService(c, user));
        await transfers.LinkAsync(new() { FirstTransactionId = 1, SecondTransactionId = 3, ExpectedFirstEditVersion = 0, ExpectedSecondEditVersion = 0 });
        Assert.False(Assert.Single(await Service(db).HistoryAsync(1)).NeedsReview); Assert.False(Assert.Single(await Service(db).HistoryAsync(2)).NeedsReview);
        var fresh = db.Context(); var management = new TransactionManagementService(fresh, user, new AccountBalanceSnapshotService(fresh, user));
        var details = await management.GetAsync(1); var t = details.Transaction;
        await management.UpdateAsync(1, new()
        {
            ExpectedEditVersion = t.EditVersion,
            ExpectedPairedEditVersion = details.PairedEditVersion,
            AccountId = t.AccountId,
            CategoryType = CategoryType.Transfer,
            Amount = 120,
            Date = DateOnly.FromDateTime(t.TransactionDate),
            Description = t.Description,
            Direction = t.TransferDirection,
            Counterparty = t.TransferCounterparty
        });
        Assert.True(Assert.Single(await Service(db).HistoryAsync(1)).NeedsReview); Assert.True(Assert.Single(await Service(db).HistoryAsync(2)).NeedsReview);
        Assert.Equal(3, await db.Context().Transactions.CountAsync());
    }
}
