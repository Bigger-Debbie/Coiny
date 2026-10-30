using Coiny.API.Data;
using Coiny.API.DTOs.Accounts;
using Coiny.API.Enums;
using Coiny.API.Interfaces;
using Coiny.API.Models;
using Coiny.API.Services;
using Microsoft.EntityFrameworkCore;
namespace Coiny.API.Tests;
public class MortgagePreferenceTests
{
    private sealed class User(string id = "one") : ICurrentUserService {
        public string? GetUserId() => id; public bool IsAuthenticated() => true;
        public Task<int> GetCurrentHouseholdIdAsync() => Task.FromResult(1);
    }
    private static DashboardService Dashboard(BudgetTestDatabase db, string userId = "one") {
        var c = db.Context(); var user = new User(userId); var snapshots = new AccountBalanceSnapshotService(c, user);
        return new(new AccountService(c, user, new AccountBalanceService(c), snapshots), new TransactionService(c, user, snapshots),
            new AccountTrendService(c, user, snapshots, new TrendService(), db.Clock), db.Clock, new UserPreferencesService(c, user));
    }
    private static async Task Seed(BudgetTestDatabase db) {
        var c = db.Context(); c.Users.AddRange(new ApplicationUser { Id = "one", UserName = "one" }, new ApplicationUser { Id = "two", UserName = "two" });
        c.Accounts.AddRange(new Account { Id = 1, HouseholdId = 1, Name = "Cash", AccountType = AccountType.Checking, OpeningBalance = 5000, OpeningBalanceDate = new(2026,10,1) },
            new Account { Id = 2, HouseholdId = 1, Name = "Home", AccountType = AccountType.Loan, LoanSubtype = LoanSubtype.Mortgage, OpeningBalance = 1000, OpeningBalanceDate = new(2026,10,1) },
            new Account { Id = 3, HouseholdId = 1, Name = "Car", AccountType = AccountType.Loan, LoanSubtype = LoanSubtype.Auto, OpeningBalance = 200, OpeningBalanceDate = new(2026,10,1) },
            new Account { Id = 4, HouseholdId = 1, Name = "Card", AccountType = AccountType.CreditCard, OpeningBalance = 100, OpeningBalanceDate = new(2026,10,1) },
            new Account { Id = 5, HouseholdId = 2, Name = "Foreign mortgage", AccountType = AccountType.Loan, LoanSubtype = LoanSubtype.Mortgage, OpeningBalance = 99999, OpeningBalanceDate = new(2026,10,1) });
        c.Categories.AddRange(new Category { Id = 1, HouseholdId = 1, Name = "Pay", CategoryType = CategoryType.Income }, new Category { Id = 2, HouseholdId = 1, Name = "Transfer", CategoryType = CategoryType.Transfer });
        c.Transactions.AddRange(new Transaction { HouseholdId = 1, AccountId = 1, CategoryId = 1, Amount = 200, Description = "Pay", TransactionDate = new(2026,10,16) },
            new Transaction { HouseholdId = 1, AccountId = 2, CategoryId = 2, Amount = 10, Description = "Payment", TransactionDate = new(2026,10,16), TransferDirection = TransferDirection.In, TransferCounterparty = TransferCounterparty.External });
        c.AccountBalanceSnapshots.AddRange(new() { AccountId = 1, SnapshotDate = new(2026,10,16), Balance = 5200 }, new() { AccountId = 2, SnapshotDate = new(2026,10,16), Balance = 990 });
        await c.SaveChangesAsync();
    }
    [Fact] public async Task Recent_transactions_stay_at_ten_for_large_households_and_keep_newest_first()
    {
        await using var db = await BudgetTestDatabase.CreateAsync(); await Seed(db);
        var c = db.Context();
        for (var i = 0; i < 20; i++) c.Transactions.Add(new() { HouseholdId = 1, AccountId = 1, CategoryId = 1, Amount = 1, Description = $"Activity {i}", TransactionDate = new(2026,10,16) });
        c.Categories.Add(new() { Id = 3, HouseholdId = 2, Name = "Foreign pay", CategoryType = CategoryType.Income });
        c.Transactions.Add(new() { HouseholdId = 2, AccountId = 5, CategoryId = 3, Amount = 999, Description = "Foreign", TransactionDate = new(2026,10,17) });
        await c.SaveChangesAsync();
        Assert.Equal(10, (await Dashboard(db).GetSummaryAsync()).RecentTransactions.Count);
        for (var i = 20; i < 30; i++) c.Accounts.Add(new() { Id = i, HouseholdId = 1, Name = $"Savings {i}", AccountType = AccountType.Savings, OpeningBalanceDate = new(2026,10,1) });
        await c.SaveChangesAsync();
        var summary = await Dashboard(db).GetSummaryAsync();
        Assert.Equal(14, summary.Accounts.Count); Assert.Equal(10, summary.RecentTransactions.Count);
        Assert.DoesNotContain(summary.RecentTransactions, t => t.Description == "Foreign");
        Assert.Equal(summary.RecentTransactions.OrderByDescending(t => t.Id).Select(t => t.Id), summary.RecentTransactions.Select(t => t.Id));
    }
    [Fact] public async Task Investment_totals_are_separate_household_scoped_and_visible_even_at_zero() {
        await using var db = await BudgetTestDatabase.CreateAsync(); await Seed(db);
        Assert.False((await Dashboard(db).GetSummaryAsync()).NetWorth.HasInvestments);
        var c = db.Context();
        c.Accounts.AddRange(new Account { Id = 10, HouseholdId = 1, Name = "Brokerage", AccountType = AccountType.Investment, OpeningBalance = 2500, OpeningBalanceDate = new(2026,10,1) },
            new Account { Id = 11, HouseholdId = 2, Name = "Foreign", AccountType = AccountType.Investment, OpeningBalance = 99999, OpeningBalanceDate = new(2026,10,1) }); await c.SaveChangesAsync();
        var summary = (await Dashboard(db).GetSummaryAsync()).NetWorth;
        Assert.True(summary.HasInvestments); Assert.Equal(2500, summary.Investments);
        Assert.Equal(6410, summary.CurrentValue); Assert.Equal(0, summary.Retirement); Assert.Equal(5200, summary.LiquidFunds);
        (await c.Accounts.FindAsync(10))!.OpeningBalance = 0; await c.SaveChangesAsync();
        summary = (await Dashboard(db).GetSummaryAsync()).NetWorth;
        Assert.True(summary.HasInvestments); Assert.Equal(0, summary.Investments);
    }
    [Fact] public async Task Account_edit_rebuilds_history_and_keeps_transactions_intact() {
        await using var db = await BudgetTestDatabase.CreateAsync(); await Seed(db); var c = db.Context(); var user = new User();
        c.AccountBalanceSnapshots.Add(new() { AccountId = 1, SnapshotDate = new(2026,10,1), Balance = 5000 }); await c.SaveChangesAsync();
        var service = new AccountService(c, user, new AccountBalanceService(c), new AccountBalanceSnapshotService(c,user));
        var result = await service.UpdateAccountAsync(1, new() { Name = " New checking ", AccountType = AccountType.Checking,
            OpeningBalance = 1000, OpeningBalanceDate = new(2026,10,5) });
        Assert.Equal("New checking", result.Name); Assert.Equal(1200, result.CurrentBalance);
        Assert.False(await c.AccountBalanceSnapshots.AnyAsync(s => s.AccountId == 1 && s.SnapshotDate < new DateOnly(2026,10,5)));
        Assert.Equal(1000, (await c.AccountBalanceSnapshots.SingleAsync(s => s.AccountId == 1 && s.SnapshotDate == new DateOnly(2026,10,5))).Balance);
        Assert.Equal(1200, (await c.AccountBalanceSnapshots.SingleAsync(s => s.AccountId == 1 && s.SnapshotDate == new DateOnly(2026,10,16))).Balance);
        Assert.Equal(200, (await c.Transactions.SingleAsync(t => t.AccountId == 1)).Amount);
    }
    [Fact] public async Task Account_edit_rejects_foreign_accounts_type_changes_invalid_dates_and_foreign_institutions() {
        await using var db = await BudgetTestDatabase.CreateAsync(); await Seed(db); var c = db.Context(); var user = new User();
        c.Institutions.Add(new Institution { Id = 50, HouseholdId = 2, Name = "Foreign" }); await c.SaveChangesAsync();
        var service = new AccountService(c, user, new AccountBalanceService(c), new AccountBalanceSnapshotService(c,user));
        CreateAccountRequest Valid() => new() { Name = "Checking", AccountType = AccountType.Checking, OpeningBalance = 5000, OpeningBalanceDate = new(2026,10,1) };
        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.UpdateAccountAsync(5, Valid()));
        var change = Valid(); change.AccountType = AccountType.Loan; await Assert.ThrowsAsync<ArgumentException>(() => service.UpdateAccountAsync(1,change));
        change = Valid(); change.OpeningBalanceDate = new(2026,10,17); await Assert.ThrowsAsync<ArgumentException>(() => service.UpdateAccountAsync(1,change));
        change = Valid(); change.OpeningBalance = 1.001m; await Assert.ThrowsAsync<ArgumentException>(() => service.UpdateAccountAsync(1,change));
        change = Valid(); change.InstitutionId = 50; await Assert.ThrowsAsync<ArgumentException>(() => service.UpdateAccountAsync(1,change));
        using var verify = db.Context(); Assert.Equal(5000, (await verify.Accounts.FindAsync(1))!.OpeningBalance);
    }

    [Fact] public async Task Mortgage_pill_is_separate_but_included_by_default_and_counts_once() {
        await using var db = await BudgetTestDatabase.CreateAsync(); await Seed(db);
        var summary = await Dashboard(db).GetSummaryAsync();
        Assert.True(summary.NetWorth.IncludeMortgageInNetWorth); Assert.True(summary.NetWorth.HasMortgage);
        Assert.Equal(990, summary.NetWorth.Mortgage); Assert.Equal(300, summary.NetWorth.Debt); Assert.Equal(3910, summary.NetWorth.CurrentValue);
        var trend = await Dashboard(db).GetNetWorthTrendAsync(new(2026,10,15), new(2026,10,16));
        Assert.Equal(3700, trend.StartingValue); Assert.Equal(3910, trend.EndingValue); Assert.Equal(210, trend.AbsoluteChange);
    }
    [Fact] public async Task Preference_is_per_user_persisted_and_applies_to_all_chart_metrics_without_changing_ledger() {
        await using var db = await BudgetTestDatabase.CreateAsync(); await Seed(db);
        await new UserPreferencesService(db.Context(), new User()).SaveAsync(false);
        Assert.False((await new UserPreferencesService(db.Context(), new User()).GetAsync()).IncludeMortgageInNetWorth);
        Assert.True((await new UserPreferencesService(db.Context(), new User("two")).GetAsync()).IncludeMortgageInNetWorth);
        var summary = await Dashboard(db).GetSummaryAsync(); Assert.Equal(4900, summary.NetWorth.CurrentValue); Assert.Equal(990, summary.NetWorth.Mortgage);
        var trend = await Dashboard(db).GetNetWorthTrendAsync(new(2026,10,15), new(2026,10,16));
        Assert.Equal(4700, trend.StartingValue); Assert.Equal(4900, trend.EndingValue); Assert.Equal(200, trend.AbsoluteChange); Assert.NotNull(trend.PercentageChange);
        Assert.Equal(3910, (await Dashboard(db,"two").GetSummaryAsync()).NetWorth.CurrentValue);
        Assert.Equal(2, await db.Context().Transactions.CountAsync()); Assert.Equal(1000, (await db.Context().Accounts.SingleAsync(a => a.Id == 2)).OpeningBalance);
    }
    [Fact] public async Task Mortgage_flag_stays_true_when_paid_off_and_is_false_without_a_household_mortgage() {
        await using var db = await BudgetTestDatabase.CreateAsync(); await Seed(db); var c = db.Context();
        var home = await c.Accounts.SingleAsync(a => a.Id == 2); home.LoanSubtype = LoanSubtype.Other; await c.SaveChangesAsync();
        var noMortgage = await Dashboard(db).GetSummaryAsync(); Assert.False(noMortgage.NetWorth.HasMortgage); Assert.Equal(0, noMortgage.NetWorth.Mortgage);
        home.LoanSubtype = LoanSubtype.Mortgage; home.OpeningBalance = 10; await c.SaveChangesAsync();
        var paid = await Dashboard(db).GetSummaryAsync(); Assert.True(paid.NetWorth.HasMortgage); Assert.Equal(0, paid.NetWorth.Mortgage);
    }
    [Fact] public async Task Creation_defaults_loans_to_other_and_rejects_subtypes_on_nonloans() {
        await using var db = await BudgetTestDatabase.CreateAsync(); var c = db.Context(); var user = new User();
        var service = new AccountService(c, user, new AccountBalanceService(c), new AccountBalanceSnapshotService(c,user));
        var request = new CreateAccountRequest { Name = "Loan", AccountType = AccountType.Loan, OpeningBalanceDate = new(2026,10,1) };
        Assert.Equal(LoanSubtype.Other, (await service.CreateAccountAsync(request)).LoanSubtype);
        request.AccountType = AccountType.Checking; request.LoanSubtype = LoanSubtype.Mortgage;
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAccountAsync(request));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => new UserPreferencesService(c,new User("missing")).GetAsync());
    }
}
