using Coiny.API.DTOs.Transactions;
using Coiny.API.Enums;
using Coiny.API.Interfaces;
using Coiny.API.Models;
using Coiny.API.Services;
using Microsoft.EntityFrameworkCore;
namespace Coiny.API.Tests;

public class MarketAdjustmentTests
{
    private sealed class User(int household = 1) : ICurrentUserService {
        public string? GetUserId() => "test";
        public bool IsAuthenticated() => true;
        public Task<int> GetCurrentHouseholdIdAsync() => Task.FromResult(household);
    }
    private static MarketAdjustmentService Service(BudgetTestDatabase db, int household = 1) {
        var c = db.Context(); var u = new User(household); return new(c, u, new AccountBalanceSnapshotService(c, u));
    }
    private static TransactionManagementService Management(BudgetTestDatabase db) {
        var c = db.Context(); var u = new User(); return new(c, u, new AccountBalanceSnapshotService(c, u));
    }
    private static async Task Seed(BudgetTestDatabase db, AccountType type = AccountType.Investment) {
        var c = db.Context();
        c.Accounts.AddRange(new Account { Id = 1, HouseholdId = 1, Name = "Investments", AccountType = type, IsActive = true, OpeningBalance = 20000, OpeningBalanceDate = new(2026,10,1) },
            new Account { Id = 2, HouseholdId = 2, Name = "Foreign", AccountType = AccountType.Investment, IsActive = true, OpeningBalanceDate = new(2026,10,1) });
        c.Categories.Add(new Category { Id = 1, HouseholdId = 1, Name = "Interest", CategoryType = CategoryType.Income, IsActive = true });
        c.Transactions.Add(new Transaction { AccountId = 1, HouseholdId = 1, CategoryId = 1, Amount = 100, TransactionDate = new(2026,10,20), Description = "Later dividend" });
        await c.SaveChangesAsync();
    }
    private static MarketAdjustmentRequest Request(decimal target, decimal expected = 20000) => new() { AccountId = 1, Date = new(2026,10,10), UpdatedBalance = target, ExpectedBalance = expected, Notes = "Statement value" };
    [Theory]
    [InlineData(AccountType.Investment, 20450, 450)]
    [InlineData(AccountType.Retirement, 19800, -200)]
    [InlineData(AccountType.Crypto, 0, -20000)]
    public async Task Dated_gains_and_losses_rebuild_balances_and_history_without_transfers(AccountType type, int target, int delta) {
        await using var db = await BudgetTestDatabase.CreateAsync(); await Seed(db, type);
        Assert.Equal(20000, (await Service(db).GetBalanceAsync(1, new(2026,10,10), null)).Balance);
        var saved = await Service(db).CreateAsync(Request(target));
        Assert.Equal(CategoryType.MarketAdjustment, saved.CategoryType); Assert.Equal(delta, saved.Amount);
        Assert.Equal(delta > 0 ? "Market gain" : "Market loss", saved.Description); Assert.Null(saved.PairedTransactionId);
        Assert.Equal(target + 100, await new AccountBalanceService(db.Context()).GetCurrentBalanceAsync(1));
        Assert.Equal(target, (await Service(db).GetBalanceAsync(1, new(2026,10,10), null)).Balance);
        Assert.Equal(20000, (await Service(db).GetBalanceAsync(1, new(2026,10,9), null)).Balance);
        Assert.Equal(target, (await db.Context().AccountBalanceSnapshots.AsNoTracking().SingleAsync(s => s.SnapshotDate == new DateOnly(2026,10,10))).Balance);
        Assert.Equal(2, await db.Context().Transactions.CountAsync());
        await Management(db).DeleteAsync(saved.Id, saved.EditVersion, null);
        Assert.Equal(20100, await new AccountBalanceService(db.Context()).GetCurrentBalanceAsync(1));
    }
    [Fact] public async Task Ownership_type_precision_and_stale_previews_are_enforced_without_writes() {
        await using var db = await BudgetTestDatabase.CreateAsync(); await Seed(db);
        var r = Request(21000); r.AccountId = 2;
        await Assert.ThrowsAsync<ArgumentException>(() => Service(db).CreateAsync(r));
        await Assert.ThrowsAsync<ArgumentException>(() => Service(db, 2).GetBalanceAsync(1, r.Date, null));
        r.AccountId = 1; r.Date = new(2026,9,30); await Assert.ThrowsAsync<ArgumentException>(() => Service(db).CreateAsync(r));
        r.Date = new(2026,10,10); r.UpdatedBalance = -1; await Assert.ThrowsAsync<ArgumentException>(() => Service(db).CreateAsync(r));
        r.UpdatedBalance = 20100.001m; await Assert.ThrowsAsync<ArgumentException>(() => Service(db).CreateAsync(r));
        r.UpdatedBalance = 20000; await Assert.ThrowsAsync<ArgumentException>(() => Service(db).CreateAsync(r));
        r.UpdatedBalance = 21000; r.ExpectedBalance = 20001; await Assert.ThrowsAsync<TransactionConflictException>(() => Service(db).CreateAsync(r));
        var c = db.Context(); var a = await c.Accounts.SingleAsync(a => a.Id == 1); a.AccountType = AccountType.Checking; await c.SaveChangesAsync();
        await Assert.ThrowsAsync<ArgumentException>(() => Service(db).CreateAsync(Request(21000)));
        Assert.Single(await db.Context().Transactions.ToListAsync());
        Assert.DoesNotContain(await db.Context().Categories.ToListAsync(), c => c.CategoryType == CategoryType.MarketAdjustment);
    }
    [Fact] public async Task Repeated_updates_use_the_dated_balance_and_edits_exclude_the_original_adjustment() {
        await using var db = await BudgetTestDatabase.CreateAsync(); await Seed(db);
        var first = await Service(db).CreateAsync(Request(20450));
        await Assert.ThrowsAsync<TransactionConflictException>(() => Service(db).CreateAsync(Request(20450)));
        var next = await Service(db).CreateAsync(Request(19800, 20450)); Assert.Equal(-650, next.Amount);
        var details = await Management(db).GetAsync(next.Id); Assert.Equal(19800, details.AdjustmentBalance);
        var r = new UpdateTransactionRequest { ExpectedEditVersion = details.Transaction.EditVersion, AccountId = 1, CategoryType = CategoryType.MarketAdjustment,
            Amount = next.Amount, Date = new(2026,10,10), Description = next.Description, UpdatedBalance = 19900, ExpectedBalance = 20450, Notes = "Corrected" };
        var saved = await Management(db).UpdateAsync(next.Id, r); Assert.Equal(-550, saved.Transaction.Amount); Assert.Equal(19900, saved.AdjustmentBalance);
        Assert.Equal(20000, await new AccountBalanceService(db.Context()).GetCurrentBalanceAsync(1));
        await Assert.ThrowsAsync<TransactionConflictException>(() => Management(db).UpdateAsync(next.Id, r));
        r.ExpectedEditVersion = saved.Transaction.EditVersion; r.CategoryType = CategoryType.Income; r.CategoryId = 1; r.Amount = 100;
        await Assert.ThrowsAsync<ArgumentException>(() => Management(db).UpdateAsync(next.Id, r));
    }
    [Fact] public async Task Ordinary_creation_cannot_bypass_the_balance_entry_flow() {
        await using var db = await BudgetTestDatabase.CreateAsync(); await Seed(db);
        await Service(db).CreateAsync(Request(20450));
        var c = db.Context(); var category = await c.Categories.SingleAsync(c => c.CategoryType == CategoryType.MarketAdjustment);
        var u = new User(); var ordinary = new TransactionService(c, u, new AccountBalanceSnapshotService(c, u));
        await Assert.ThrowsAsync<InvalidOperationException>(() => ordinary.CreateTransactionAsync(new() { AccountId = 1, CategoryId = category.Id, Amount = 100, TransactionDate = new(2026,10,11), Description = "Bypass" }));
    }
}
