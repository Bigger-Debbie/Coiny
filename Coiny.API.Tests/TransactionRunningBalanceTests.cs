using Coiny.API.Data;
using Coiny.API.DTOs.Transactions;
using Coiny.API.Enums;
using Coiny.API.Interfaces;
using Coiny.API.Models;
using Coiny.API.Services;
using Microsoft.EntityFrameworkCore;

namespace Coiny.API.Tests;

public class TransactionRunningBalanceTests
{
    private static readonly DateOnly OpeningDay = new(2026, 10, 1);
    private sealed class User(int household = 1) : ICurrentUserService {
        public string? GetUserId() => "test";
        public bool IsAuthenticated() => true;
        public Task<int> GetCurrentHouseholdIdAsync() => Task.FromResult(household);
    }
    private static TransactionService Service(CoinyContext c, int household = 1) => new(c, new User(household), new AccountBalanceSnapshotService(c, new User(household)));
    private static async Task<Account> Account(CoinyContext c, AccountType type = AccountType.Checking, int household = 1)
    {
        var account = new Account { HouseholdId = household, Name = "Test account", AccountType = type, OpeningBalance = 100, OpeningBalanceDate = OpeningDay };
        c.Accounts.Add(account); await c.SaveChangesAsync(); return account;
    }
    private static async Task<Transaction> Entry(CoinyContext c, Account account, CategoryType type, decimal amount, int day,
        string description = "visible", TransferDirection? direction = null)
    {
        var category = new Category { HouseholdId = account.HouseholdId, Name = "Test " + type + " " + Guid.NewGuid().ToString("N"), CategoryType = type };
        c.Categories.Add(category); await c.SaveChangesAsync();
        var entry = new Transaction { HouseholdId = account.HouseholdId, AccountId = account.Id, CategoryId = category.Id,
            TransactionDate = new DateTime(2026, 10, day), Amount = amount, Description = description, TransferDirection = direction,
            TransferCounterparty = direction.HasValue ? TransferCounterparty.Unknown : null };
        c.Transactions.Add(entry); await c.SaveChangesAsync(); return entry;
    }

    [Fact]
    public async Task FiltersAndPagesDoNotResetBalancesOrExcludeHiddenHistory()
    {
        await using var db = await BudgetTestDatabase.CreateAsync(); var c = db.Context(); var account = await Account(c);
        await Entry(c, account, CategoryType.Income, 50, 2);
        await Entry(c, account, CategoryType.Expense, 20, 2, "hidden");
        var expense = await Entry(c, account, CategoryType.Expense, 10, 3);
        await Entry(c, account, CategoryType.Income, 5, 3, "hidden");
        var transfer = await Entry(c, account, CategoryType.Transfer, 7, 3, direction: TransferDirection.Out);
        await Entry(c, account, CategoryType.Income, 1000, 4, "hidden future");
        await Entry(c, await Account(c), CategoryType.Income, 9999, 2);
        await Entry(c, await Account(c, household: 2), CategoryType.Income, 8888, 2);
        var service = Service(c);
        var query = new TransactionQuery { AccountId = account.Id, StartDate = new(2026,10,3), EndDate = new(2026,10,3), Search = "visible", PageSize = 1 };
        var first = await service.GetPageAsync(query);
        Assert.Equal(2, first.TotalCount); Assert.Equal(transfer.Id, first.Items.Single().Id); Assert.Equal(118m, first.Items.Single().RunningBalance);
        query.Page = 2; var second = await service.GetPageAsync(query);
        Assert.Equal(expense.Id, second.Items.Single().Id); Assert.Equal(120m, second.Items.Single().RunningBalance);
        query.CategoryType = CategoryType.Expense; query.Page = 99;
        var filtered = await service.GetPageAsync(query);
        Assert.Equal(1, filtered.Page); Assert.Equal(120m, filtered.Items.Single().RunningBalance);
        var complete = await service.GetPageAsync(new() { AccountId = account.Id });
        Assert.Equal(await new AccountBalanceService(c).GetCurrentBalanceAsync(account.Id), complete.Items.First().RunningBalance);
    }

    [Theory]
    [InlineData(AccountType.Checking, false)]
    [InlineData(AccountType.Savings, false)]
    [InlineData(AccountType.Investment, false)]
    [InlineData(AccountType.CreditCard, true)]
    [InlineData(AccountType.Loan, true)]
    public async Task UsesExistingBalanceRulesForAssetsAndAmountsOwed(AccountType type, bool debt)
    {
        await using var db = await BudgetTestDatabase.CreateAsync(); var c = db.Context(); var account = await Account(c, type);
        // Uncleared transactions count, just as they do in the current account balance.
        await Entry(c, account, CategoryType.Expense, 25.25m, 1);
        await Entry(c, account, CategoryType.Transfer, 40.50m, 2, direction: TransferDirection.In);
        await Entry(c, account, CategoryType.Transfer, 10.75m, 3, direction: TransferDirection.Out);
        await Entry(c, account, CategoryType.MarketAdjustment, -5m, 4);
        await Entry(c, account, CategoryType.Income, 10m, 5);
        var page = await Service(c).GetPageAsync(new() { AccountId = account.Id });
        var expected = debt ? new decimal[] { 125.25m, 84.75m, 95.50m, 100.50m, 90.50m } : [74.75m, 115.25m, 104.50m, 99.50m, 109.50m];
        Assert.Equal(expected, page.Items.AsEnumerable().Reverse().Select(x => x.RunningBalance!.Value));
        Assert.Equal(await new AccountBalanceService(c).GetCurrentBalanceAsync(account.Id), page.Items.First().RunningBalance);
    }

    [Fact]
    public async Task BackdatedEditsAndDeletesRecalculateWithoutStoredRunningTotals()
    {
        await using var db = await BudgetTestDatabase.CreateAsync(); var c = db.Context(); var account = await Account(c);
        var latest = await Entry(c, account, CategoryType.Expense, 30, 5);
        Assert.Equal(70m, (await Service(c).GetPageAsync(new() { AccountId = account.Id })).Items.Single().RunningBalance);
        var prior = await Entry(c, account, CategoryType.Income, 10, 2);
        var rows = await Service(c).GetPageAsync(new() { AccountId = account.Id });
        Assert.Equal(80m, rows.Items.Single(x => x.Id == latest.Id).RunningBalance);
        prior.Amount = 20; await c.SaveChangesAsync();
        Assert.Equal(90m, (await Service(c).GetPageAsync(new() { AccountId = account.Id })).Items.First().RunningBalance);
        c.Transactions.Remove(prior); await c.SaveChangesAsync();
        Assert.Equal(70m, (await Service(c).GetPageAsync(new() { AccountId = account.Id })).Items.Single().RunningBalance);
    }

    [Fact]
    public async Task EmptyAndAllAccountPagesHaveNoInventedBalancesAndForeignAccountIsRejected()
    {
        await using var db = await BudgetTestDatabase.CreateAsync(); var c = db.Context(); var account = await Account(c);
        Assert.Empty((await Service(c).GetPageAsync(new() { AccountId = account.Id })).Items);
        await Entry(c, account, CategoryType.Income, 1, 2);
        Assert.All((await Service(c).GetPageAsync(new())).Items, x => Assert.Null(x.RunningBalance));
        await Assert.ThrowsAsync<InvalidOperationException>(() => Service(c, 2).GetPageAsync(new() { AccountId = account.Id }));
    }
}
