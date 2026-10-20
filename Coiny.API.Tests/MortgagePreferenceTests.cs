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
