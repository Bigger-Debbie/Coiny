using Coiny.API.Data;
using Coiny.API.DTOs.Budgets;
using Coiny.API.Enums;
using Coiny.API.Interfaces;
using Coiny.API.Models;
using Coiny.API.Services;
using Microsoft.EntityFrameworkCore;

namespace Coiny.API.Tests;

public class ContributionPaymentTests
{
    private static readonly DateOnly Due = new(2026, 10, 17);
    private sealed class User : ICurrentUserService
    {
        public string? GetUserId() => "test";
        public bool IsAuthenticated() => true;
        public Task<int> GetCurrentHouseholdIdAsync() => Task.FromResult(1);
    }
    private static BillPaymentService Payments(BudgetTestDatabase db) => new(db.Context(), new User(), db.Service(), db.Clock);
    private static TransferService Transfers(CoinyContext c) => new(c, new User(), new AccountBalanceSnapshotService(c, new User()));
    private static async Task<Dictionary<string, Guid>> Plan(BudgetTestDatabase db)
    {
        var service = db.Service(); var draft = await service.StartDraftAsync();
        BudgetAllocationInput Allocation(string name, BudgetAllocationType type, bool scheduled = true) => new()
        {
            Name = name, Amount = 100, AllocationType = type,
            Schedule = scheduled ? new() { Frequency = BudgetFrequency.Monthly, StartDate = new(2026, 10, 1), DayOfMonth = 17 } : null
        };
        var saved = await service.SaveDraftAsync(draft.Id, new()
        {
            ExpectedEditVersion = draft.EditVersion, EffectiveFromMonth = new(2026, 10, 1), SetupStep = BudgetSetupStep.Review,
            IncomeSources = [new() { Name = "Salary", Amount = 2000, Schedule = new() { Frequency = BudgetFrequency.EveryTwoWeeks, StartDate = new(2026, 10, 16) } }],
            Allocations = [Allocation("IRA", BudgetAllocationType.Investments), Allocation("Emergency fund", BudgetAllocationType.Savings),
                Allocation("Electric", BudgetAllocationType.Bills), Allocation("Groceries", BudgetAllocationType.Spending), Allocation("Unscheduled", BudgetAllocationType.Investments, false)]
        });
        await service.PublishDraftAsync(saved.Id, saved.EditVersion);
        return saved.Allocations.ToDictionary(a => a.Name, a => a.SourceKey!.Value);
    }
    private static async Task Ledger(BudgetTestDatabase db)
    {
        var c = db.Context();
        c.Accounts.AddRange(new Account { Id = 1, HouseholdId = 1, Name = "Checking", OpeningBalance = 1000, OpeningBalanceDate = new(2026, 10, 1) },
            new Account { Id = 2, HouseholdId = 1, Name = "IRA", AccountType = AccountType.Investment, OpeningBalance = 500, OpeningBalanceDate = new(2026, 10, 1) },
            new Account { Id = 3, HouseholdId = 2, Name = "Foreign", OpeningBalanceDate = new(2026, 10, 1) });
        c.Categories.AddRange(new Category { Id = 1, HouseholdId = 1, Name = "Broker payment", CategoryType = CategoryType.Expense, IsActive = true },
            new Category { Id = 2, HouseholdId = 1, Name = "Income", CategoryType = CategoryType.Income, IsActive = true },
            new Category { Id = 3, HouseholdId = 2, Name = "Foreign expense", CategoryType = CategoryType.Expense, IsActive = true });
        c.Transactions.AddRange(new Transaction { Id = 1, HouseholdId = 1, AccountId = 1, CategoryId = 1, Amount = 100, TransactionDate = new(2026, 10, 15), Description = "Broker payment" },
            new Transaction { Id = 2, HouseholdId = 1, AccountId = 1, CategoryId = 2, Amount = 100, TransactionDate = new(2026, 10, 15), Description = "Income" },
            new Transaction { Id = 3, HouseholdId = 2, AccountId = 3, CategoryId = 3, Amount = 100, TransactionDate = new(2026, 10, 15), Description = "Foreign" });
        await c.SaveChangesAsync();
    }
    [Fact]
    public async Task Manual_contribution_payment_persists_in_month_and_period_without_changing_bills_or_ledger()
    {
        await using var db = await BudgetTestDatabase.CreateAsync(); var keys = await Plan(db);
        var payment = await Payments(db).SaveAsync(keys["IRA"], Due, new() { IsPaid = true, Note = " Completed " });
        Assert.True(payment.IsPaid); Assert.Equal("Completed", payment.Note);
        Assert.Equal("AwaitingTransaction", payment.Status);
        var month = await db.Service().GetMonthAsync(new(2026, 10, 1)); Assert.True(Assert.Single(month.Events, e => e.Name == "IRA").Payment!.IsPaid);
        var period = await db.Service().GetIncomePeriodAsync(); Assert.Equal(200, period.ContributionsTotal); Assert.Equal(100, period.UnpaidContributionsTotal);
        Assert.Equal(100, period.UnpaidBillsTotal); Assert.True(Assert.Single(period.Contributions, e => e.Name == "IRA").Payment!.IsPaid);
        Assert.Equal(1, period.AwaitingTransactionCount); Assert.Equal(100, period.AwaitingTransactionTotal);
        await Payments(db).SaveAsync(keys["Electric"], Due, new() { IsPaid = true });
        Assert.Equal(2, (await db.Service().GetIncomePeriodAsync()).AwaitingTransactionCount);
        Assert.Equal(200, (await db.Service().GetIncomePeriodAsync()).AwaitingTransactionTotal);
        Assert.Null(Assert.Single((await db.Service().GetMonthAsync(new(2026, 11, 1))).Events, e => e.Name == "IRA").Payment);
        await Payments(db).SaveAsync(keys["IRA"], Due, new() { IsPaid = false });
        Assert.Equal(200, (await db.Service().GetIncomePeriodAsync()).UnpaidContributionsTotal);
        Assert.Empty(await db.Context().Transactions.ToListAsync()); Assert.Empty(await db.Context().AccountBalanceSnapshots.ToListAsync());
    }
    [Fact]
    public async Task Linking_either_transfer_side_preserves_ledger_and_reserves_the_entire_pair_for_one_occurrence()
    {
        await using var db = await BudgetTestDatabase.CreateAsync(); var keys = await Plan(db); await Ledger(db); var c = db.Context();
        var pair = await Transfers(c).CreateAsync(new() { FromAccountId = 1, ToAccountId = 2, Amount = 100, Date = new(2026, 10, 15) });
        var snapshots = await c.AccountBalanceSnapshots.AsNoTracking().OrderBy(s => s.Id).Select(s => new { s.AccountId, s.SnapshotDate, s.Balance }).ToArrayAsync();
        var linked = await Payments(db).SaveAsync(keys["IRA"], Due, new() { IsPaid = true, TransactionId = pair.Incoming.Id });
        Assert.Equal(new DateOnly(2026, 10, 15), linked.PaidOn);
        Assert.Equal("Linked", linked.Status); Assert.Equal(0, (await db.Service().GetIncomePeriodAsync()).AwaitingTransactionCount);
        await Assert.ThrowsAsync<BudgetConflictException>(() => Payments(db).SaveAsync(keys["Emergency fund"], Due, new() { IsPaid = true, TransactionId = pair.Outgoing.Id }));
        Assert.DoesNotContain(await Payments(db).CandidatesAsync(keys["Emergency fund"], Due, null), t => t.Id == pair.Incoming.Id || t.Id == pair.Outgoing.Id);
        var switched = await Payments(db).SaveAsync(keys["IRA"], Due, new() { IsPaid = true, TransactionId = pair.Outgoing.Id }); Assert.Equal(pair.Outgoing.Id, switched.TransactionId);
        await Payments(db).SaveAsync(keys["IRA"], Due, new() { IsPaid = true });
        Assert.True(Assert.Single((await db.Service().GetIncomePeriodAsync()).Contributions, e => e.Name == "IRA").Payment!.IsPaid);
        Assert.Equal(5, await c.Transactions.CountAsync());
        Assert.Equal(snapshots, await c.AccountBalanceSnapshots.AsNoTracking().OrderBy(s => s.Id).Select(s => new { s.AccountId, s.SnapshotDate, s.Balance }).ToArrayAsync());
    }
    [Fact]
    public async Task Contributions_accept_expenses_and_transfers_but_reject_income_foreign_unscheduled_and_spending_items()
    {
        await using var db = await BudgetTestDatabase.CreateAsync(); var keys = await Plan(db); await Ledger(db);
        var pair = await Transfers(db.Context()).CreateAsync(new() { FromAccountId = 1, ToAccountId = 2, Amount = 100, Date = new(2026, 10, 15) });
        var candidates = await Payments(db).CandidatesAsync(keys["IRA"], Due, null);
        Assert.Contains(candidates, t => t.Id == 1 && t.CategoryType == CategoryType.Expense);
        Assert.Contains(candidates, t => t.Id == pair.Incoming.Id && t.CategoryType == CategoryType.Transfer && t.TransferDirection == TransferDirection.In);
        Assert.DoesNotContain(candidates, t => t.Id is 2 or 3);
        await Assert.ThrowsAsync<ArgumentException>(() => Payments(db).SaveAsync(keys["IRA"], Due, new() { IsPaid = true, TransactionId = 2 }));
        await Assert.ThrowsAsync<ArgumentException>(() => Payments(db).SaveAsync(keys["IRA"], Due, new() { IsPaid = true, TransactionId = 3 }));
        await Assert.ThrowsAsync<ArgumentException>(() => Payments(db).SaveAsync(keys["Electric"], Due, new() { IsPaid = true, TransactionId = pair.Outgoing.Id }));
        Assert.DoesNotContain(await Payments(db).CandidatesAsync(keys["Electric"], Due, null), t => t.CategoryType == CategoryType.Transfer);
        await Assert.ThrowsAsync<ArgumentException>(() => Payments(db).SaveAsync(keys["Unscheduled"], Due, new() { IsPaid = true }));
        await Assert.ThrowsAsync<ArgumentException>(() => Payments(db).SaveAsync(keys["Groceries"], Due, new() { IsPaid = true }));
        await Payments(db).SaveAsync(keys["IRA"], Due, new() { IsPaid = true, TransactionId = 1 });
    }
    [Fact]
    public async Task Linking_two_previously_assigned_entries_requires_resolving_duplicate_budget_assignments()
    {
        await using var db = await BudgetTestDatabase.CreateAsync(); var keys = await Plan(db); await Ledger(db); var c = db.Context(); var transfers = Transfers(c);
        var outgoing = await transfers.CreateEntryAsync(new() { AccountId = 1, Amount = 100, Date = new(2026, 10, 15), Direction = TransferDirection.Out, Description = "Transfer out" });
        var incoming = await transfers.CreateEntryAsync(new() { AccountId = 2, Amount = 100, Date = new(2026, 10, 15), Direction = TransferDirection.In, Description = "Transfer in" });
        await Payments(db).SaveAsync(keys["IRA"], Due, new() { IsPaid = true, TransactionId = outgoing.Id });
        await Payments(db).SaveAsync(keys["Emergency fund"], Due, new() { IsPaid = true, TransactionId = incoming.Id });
        await Assert.ThrowsAsync<TransferConflictException>(() => transfers.LinkAsync(new() { FirstTransactionId = outgoing.Id, SecondTransactionId = incoming.Id }));
        Assert.Null((await db.Context().Transactions.SingleAsync(t => t.Id == outgoing.Id)).PairedTransactionId);
        await Payments(db).SaveAsync(keys["Emergency fund"], Due, new() { IsPaid = true });
        await transfers.LinkAsync(new() { FirstTransactionId = outgoing.Id, SecondTransactionId = incoming.Id });
        await Assert.ThrowsAsync<BudgetConflictException>(() => Payments(db).SaveAsync(keys["Emergency fund"], Due, new() { IsPaid = true, TransactionId = incoming.Id }));
    }
    [Fact]
    public async Task Editing_linked_transfer_keeps_contribution_reference_and_deletion_keeps_independent_paid_status()
    {
        await using var db = await BudgetTestDatabase.CreateAsync(); var keys = await Plan(db); await Ledger(db); var c = db.Context();
        var pair = await Transfers(c).CreateAsync(new() { FromAccountId = 1, ToAccountId = 2, Amount = 100, Date = new(2026, 10, 15) });
        await Payments(db).SaveAsync(keys["IRA"], Due, new() { IsPaid = true, TransactionId = pair.Incoming.Id });
        var editContext = db.Context(); var management = new TransactionManagementService(editContext, new User(), new AccountBalanceSnapshotService(editContext, new User()));
        var details = await management.GetAsync(pair.Outgoing.Id); Assert.NotNull(details.Bill);
        var updated = await management.UpdateAsync(pair.Outgoing.Id, new() { AccountId = 1, ExpectedEditVersion = details.Transaction.EditVersion, ExpectedPairedEditVersion = details.PairedEditVersion,
            CategoryType = CategoryType.Transfer, Amount = 120, Date = new(2026, 10, 15), Description = "Updated contribution", Direction = TransferDirection.Out });
        Assert.NotNull(updated.Bill); Assert.Equal(120, Assert.Single((await db.Service().GetIncomePeriodAsync()).Contributions, e => e.Name == "IRA").Payment!.LinkedTransaction!.Amount);
        await management.DeleteAsync(pair.Outgoing.Id, updated.Transaction.EditVersion, updated.PairedEditVersion);
        var payment = Assert.Single((await db.Service().GetIncomePeriodAsync()).Contributions, e => e.Name == "IRA").Payment!;
        Assert.True(payment.IsPaid); Assert.Null(payment.TransactionId); Assert.Equal(3, await db.Context().Transactions.CountAsync());
        Assert.Equal("AwaitingTransaction", payment.Status); Assert.Equal(1, (await db.Service().GetIncomePeriodAsync()).AwaitingTransactionCount);
    }
}
