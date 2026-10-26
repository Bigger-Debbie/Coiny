using Coiny.API.DTOs.Budgets;
using Coiny.API.Enums;
using Coiny.API.Interfaces;
using Coiny.API.Models;
using Coiny.API.Services;
using Microsoft.EntityFrameworkCore;
namespace Coiny.API.Tests;
public class BillPaymentTests
{
    private static readonly DateOnly Due = new(2026,10,17);
    private sealed class User(int id) : ICurrentUserService {
        public string? GetUserId() => "test"; public bool IsAuthenticated() => true;
        public Task<int> GetCurrentHouseholdIdAsync() => Task.FromResult(id);
    }
    private static BillPaymentService Service(BudgetTestDatabase db, int household = 1) => new(db.Context(), new User(household), db.Service(household), db.Clock);
    private static async Task<Guid> Plan(BudgetTestDatabase db, BudgetAllocationType type = BudgetAllocationType.Bills)
    {
        var service = db.Service(); var draft = await service.StartDraftAsync();
        var saved = await service.SaveDraftAsync(draft.Id, new() {
            ExpectedEditVersion = draft.EditVersion, EffectiveFromMonth = new(2026,10,1), SetupStep = BudgetSetupStep.Review,
            IncomeSources = [new() { Name = "Salary", Amount = 2000, Schedule = new() { Frequency = BudgetFrequency.EveryTwoWeeks, StartDate = new(2026,10,16) } }],
            Allocations = [new() { Name = "Electric", Amount = 100, AllocationType = type,
                Schedule = new() { Frequency = BudgetFrequency.Monthly, StartDate = new(2026,10,1), DayOfMonth = 17 } }],
        });
        await service.PublishDraftAsync(saved.Id, saved.EditVersion);
        return saved.Allocations[0].SourceKey!.Value;
    }
    private static async Task Ledger(BudgetTestDatabase db)
    {
        var c = db.Context();
        c.Accounts.AddRange(new Account { Id = 1, HouseholdId = 1, Name = "Checking", OpeningBalance = 1000, OpeningBalanceDate = new(2026,10,1) }, new Account { Id = 2, HouseholdId = 2, Name = "Other" });
        c.Categories.AddRange(new Category { Id = 1, HouseholdId = 1, Name = "Utilities", CategoryType = CategoryType.Expense }, new Category { Id = 2, HouseholdId = 1, Name = "Salary", CategoryType = CategoryType.Income }, new Category { Id = 3, HouseholdId = 2, Name = "Other", CategoryType = CategoryType.Expense });
        c.Transactions.AddRange(new Transaction { Id = 1, HouseholdId = 1, AccountId = 1, CategoryId = 1, Amount = 100, TransactionDate = new(2026,10,15), Description = "Electric payment" },
            new Transaction { Id = 2, HouseholdId = 1, AccountId = 1, CategoryId = 2, Amount = 100, TransactionDate = new(2026,10,15), Description = "Income" },
            new Transaction { Id = 3, HouseholdId = 2, AccountId = 2, CategoryId = 3, Amount = 100, TransactionDate = new(2026,10,15), Description = "Other" });
        await c.SaveChangesAsync();
    }
    [Fact]
    public async Task Deleting_one_charge_preserves_remaining_link_and_paid_status()
    {
        await using var db = await BudgetTestDatabase.CreateAsync(); var key = await Plan(db); await Ledger(db);
        var c = db.Context();
        c.Transactions.Add(new Transaction { Id = 4, HouseholdId = 1, AccountId = 1, CategoryId = 1, Amount = 25, TransactionDate = new(2026,10,16), Description = "Second charge" });
        await c.SaveChangesAsync();
        await Service(db).SaveAsync(key, Due, new() { IsPaid = true, TransactionIds = [1,4] });
        await c.Transactions.Where(t => t.Id == 1).ExecuteDeleteAsync();
        var payment = Assert.Single((await db.Service().GetMonthAsync(new(2026,10,1))).Events, e => !e.IsIncome).Payment!;
        Assert.Equal("Linked", payment.Status); Assert.Equal(4, payment.TransactionId);
        await c.Transactions.Where(t => t.Id == 4).ExecuteDeleteAsync();
        payment = Assert.Single((await db.Service().GetMonthAsync(new(2026,10,1))).Events, e => !e.IsIncome).Payment!;
        Assert.Equal("AwaitingTransaction", payment.Status); Assert.True(payment.IsPaid);
    }

    [Fact]
    public async Task Multiple_charges_count_once_on_their_posting_dates_and_can_be_unlinked_individually()
    {
        await using var db = await BudgetTestDatabase.CreateAsync();
        var key = await Plan(db); await Ledger(db);
        var c = db.Context();
        c.Transactions.Add(new Transaction { Id = 4, HouseholdId = 1, AccountId = 1, CategoryId = 1, Amount = 25, TransactionDate = new(2026,10,16), Description = "Second utility charge" });
        await c.SaveChangesAsync();
        var paid = await Service(db).SaveAsync(key, Due, new() { IsPaid = true, TransactionIds = [1,4] });
        Assert.Equal(125, paid.LinkedTransactions.Sum(t => t.Amount));
        Assert.Equal(new[] { 1,4 }, paid.TransactionIds);
        var month = await db.Service().GetMonthAsync(new(2026,10,1));
        Assert.Equal(2, Assert.Single(month.Events, e => !e.IsIncome).Payment!.LinkedTransactions.Count);
        var actuals = new BudgetActualsService(db.Context(), new User(1), db.Service(), db.Clock);
        var report = await actuals.GetAsync(new(2026,10,1), "all");
        Assert.Equal(125, Assert.Single(report.Items).Actual);
        await Assert.ThrowsAsync<BudgetConflictException>(() => Service(db).SaveAsync(key, Due.AddMonths(1), new() { IsPaid = true, TransactionId = 4 }));
        Assert.DoesNotContain(await Service(db).CandidatesAsync(key, Due.AddMonths(1), null), t => t.Id == 4);
        paid = await Service(db).SaveAsync(key, Due, new() { IsPaid = true, TransactionIds = [4] });
        Assert.Equal(4, Assert.Single(paid.LinkedTransactions).Id);
        Assert.Equal(25, Assert.Single((await actuals.GetAsync(new(2026,10,1), "all")).Items).Actual);
        paid = await Service(db).SaveAsync(key, Due, new() { IsPaid = true, TransactionIds = [] });
        Assert.Equal("AwaitingTransaction", paid.Status);
        Assert.Empty(paid.LinkedTransactions);
        Assert.Equal(4, await c.Transactions.CountAsync());
        Assert.Empty(await c.AccountBalanceSnapshots.ToListAsync());
    }

    [Fact]
    public async Task Deliberately_unassigned_transaction_can_be_linked_but_active_assignments_still_block()
    {
        await using var db = await BudgetTestDatabase.CreateAsync();
        var key = await Plan(db); await Ledger(db);
        var c = db.Context();
        var assignment = new TransactionBudgetAssignment { HouseholdId = 1, TransactionId = 1, AllocationKey = key };
        c.TransactionBudgetAssignments.Add(assignment); await c.SaveChangesAsync();
        Assert.DoesNotContain(await Service(db).CandidatesAsync(key, Due, null), t => t.Id == 1);
        await Assert.ThrowsAsync<BudgetConflictException>(() => Service(db).SaveAsync(key, Due, new() { IsPaid = true, TransactionId = 1 }));
        assignment.AllocationKey = null; await c.SaveChangesAsync();
        Assert.Contains(await Service(db).CandidatesAsync(key, Due, null), t => t.Id == 1);
        var payment = await Service(db).SaveAsync(key, Due, new() { IsPaid = true, TransactionId = 1 });
        Assert.Equal(1, payment.TransactionId);
        Assert.Empty(await db.Context().TransactionBudgetAssignments.ToListAsync());
        Assert.Equal(3, await db.Context().Transactions.CountAsync());
    }
    [Theory]
    [InlineData(false, false, false)] // Removal in the current month releases the link.
    [InlineData(false, true, true)] // A future plan preserves historical payment links.
    [InlineData(true, false, true)] // Moving the same item preserves its identity and link.
    public async Task Publication_releases_removed_payment_links_but_preserves_moves_and_history(bool move, bool future, bool keep)
    {
        await using var db = await BudgetTestDatabase.CreateAsync();
        var key = await Plan(db); await Ledger(db);
        var extraContext = db.Context();
        extraContext.Transactions.Add(new Transaction { Id = 4, HouseholdId = 1, AccountId = 1, CategoryId = 1, Amount = 25, TransactionDate = new(2026,10,16), Description = "Second charge" });
        await extraContext.SaveChangesAsync();
        await Service(db).SaveAsync(key, Due, new() { IsPaid = true, TransactionIds = [1,4] });
        var budget = db.Service(); var draft = await budget.StartDraftAsync(future ? new(2026,11,1) : new(2026,10,1));
        var allocations = draft.Allocations.ToList();
        if (move) allocations[0].AllocationType = BudgetAllocationType.Subscriptions;
        else allocations.Clear();
        var saved = await budget.SaveDraftAsync(draft.Id, new() {
            ExpectedEditVersion = draft.EditVersion, EffectiveFromMonth = draft.EffectiveFromMonth,
            SetupStep = BudgetSetupStep.Review, IncomeSources = draft.IncomeSources.ToList(), Allocations = allocations,
        });
        Assert.Equal(1, (await db.Context().BillPayments.SingleAsync()).TransactionId); // Drafts do not detach.
        await budget.PublishDraftAsync(saved.Id, saved.EditVersion);
        var payment = await db.Context().BillPayments.SingleAsync();
        Assert.Equal(keep ? (int?)1 : null, payment.TransactionId);
        Assert.True(payment.IsPaid);
        Assert.Equal(4, await db.Context().Transactions.CountAsync());
        Assert.Equal(keep ? 1 : 0, await db.Context().Set<BillPaymentTransaction>().CountAsync());
        if (!keep) {
            var next = await budget.StartDraftAsync();
            var replacement = new BudgetAllocationInput { Name = "Replacement", Amount = 100, AllocationType = BudgetAllocationType.Subscriptions,
                Schedule = new() { Frequency = BudgetFrequency.Monthly, StartDate = new(2026,10,1), DayOfMonth = 17 } };
            next = await budget.SaveDraftAsync(next.Id, new() { ExpectedEditVersion = next.EditVersion, EffectiveFromMonth = next.EffectiveFromMonth,
                SetupStep = BudgetSetupStep.Review, IncomeSources = next.IncomeSources.ToList(), Allocations = [replacement] });
            await budget.PublishDraftAsync(next.Id, next.EditVersion);
            Assert.Contains(await Service(db).CandidatesAsync(next.Allocations[0].SourceKey!.Value, Due, null), t => t.Id == 1);
        }
    }

    [Theory]
    [InlineData(BudgetAllocationType.Bills)]
    [InlineData(BudgetAllocationType.Subscriptions)]
    public async Task Manual_payment_is_occurrence_specific_and_reduces_only_unpaid_total(BudgetAllocationType type)
    {
        await using var db = await BudgetTestDatabase.CreateAsync(); var key = await Plan(db, type);
        await Service(db).SaveAsync(key, Due, new() { IsPaid = true, PaidOn = new(2026,10,16), Note = " Paid online " });
        var bill = Assert.Single((await db.Service().GetMonthAsync(new(2026,10,1))).Events, e => !e.IsIncome);
        Assert.True(bill.Payment!.IsPaid); Assert.Equal("Paid online", bill.Payment.Note);
        Assert.Equal("AwaitingTransaction", bill.Payment.Status);
        Assert.Null(Assert.Single((await db.Service().GetMonthAsync(new(2026,11,1))).Events, e => !e.IsIncome).Payment);
        var period = await db.Service().GetIncomePeriodAsync(); Assert.Equal(100, period.BillsTotal); Assert.Equal(0, period.UnpaidBillsTotal);
        Assert.Equal(1, period.AwaitingTransactionCount); Assert.Equal(100, period.AwaitingTransactionTotal);
        Assert.Empty(await db.Context().Transactions.ToListAsync()); Assert.Empty(await db.Context().AccountBalanceSnapshots.ToListAsync());
        await Service(db).SaveAsync(key, Due, new() { IsPaid = false });
        var unpaid = await db.Service().GetIncomePeriodAsync();
        Assert.Equal(100, unpaid.UnpaidBillsTotal); Assert.Equal(0, unpaid.AwaitingTransactionCount);
        Assert.Equal("Unpaid", Assert.Single(unpaid.Bills).Payment!.Status);
    }
    [Fact] public async Task Link_then_unlink_keeps_paid_status_and_never_changes_ledger()
    {
        await using var db = await BudgetTestDatabase.CreateAsync(); var key = await Plan(db); await Ledger(db);
        var linked = await Service(db).SaveAsync(key, Due, new() { IsPaid = true, TransactionId = 1 });
        Assert.Equal(new DateOnly(2026,10,15), linked.PaidOn);
        Assert.Equal("Electric payment", linked.LinkedTransaction!.Description);
        Assert.Equal("Linked", linked.Status); Assert.Equal(0, (await db.Service().GetIncomePeriodAsync()).AwaitingTransactionCount);
        Assert.NotNull(Assert.Single((await db.Service().GetMonthAsync(new(2026,10,1))).Events, e => !e.IsIncome).Payment!.LinkedTransaction);
        var unlinked = await Service(db).SaveAsync(key, Due, new() { IsPaid = true, PaidOn = linked.PaidOn });
        Assert.True(unlinked.IsPaid); Assert.Null(unlinked.TransactionId); Assert.Null(unlinked.LinkedTransaction);
        Assert.Equal("AwaitingTransaction", unlinked.Status); Assert.Equal(1, (await db.Service().GetIncomePeriodAsync()).AwaitingTransactionCount);
        Assert.Equal(3, await db.Context().Transactions.CountAsync()); Assert.Empty(await db.Context().AccountBalanceSnapshots.ToListAsync());
        Assert.Equal(1, Assert.Single(await Service(db).CandidatesAsync(key, Due, null)).Id);
    }
    [Fact] public async Task Invalid_foreign_income_and_already_used_links_are_rejected()
    {
        await using var db = await BudgetTestDatabase.CreateAsync(); var key = await Plan(db); await Ledger(db);
        await Assert.ThrowsAsync<ArgumentException>(() => Service(db, 2).SaveAsync(key, Due, new() { IsPaid = true }));
        await Assert.ThrowsAsync<ArgumentException>(() => Service(db).SaveAsync(key, Due.AddDays(1), new() { IsPaid = true }));
        await Assert.ThrowsAsync<ArgumentException>(() => Service(db).SaveAsync(key, Due, new() { IsPaid = true, TransactionId = 3 }));
        await Assert.ThrowsAsync<ArgumentException>(() => Service(db).SaveAsync(key, Due, new() { IsPaid = true, TransactionId = 2 }));
        await Assert.ThrowsAsync<ArgumentException>(() => Service(db).SaveAsync(key, Due, new() { IsPaid = true, PaidOn = Due }));
        await Service(db).SaveAsync(key, Due, new() { IsPaid = true, TransactionId = 1 });
        await Assert.ThrowsAsync<BudgetConflictException>(() => Service(db).SaveAsync(key, Due.AddMonths(1), new() { IsPaid = true, TransactionId = 1 }));
        Assert.Empty(await Service(db).CandidatesAsync(key, Due.AddMonths(1), null));
        Assert.Single(await db.Context().BillPayments.ToListAsync());
    }
    [Fact] public async Task Stable_identity_survives_draft_saves_rename_and_republication()
    {
        await using var db = await BudgetTestDatabase.CreateAsync(); var key = await Plan(db);
        await Service(db).SaveAsync(key, Due, new() { IsPaid = true });
        var draft = await db.Service().StartDraftAsync(); Assert.Equal(key, draft.Allocations[0].SourceKey);
        draft.Allocations[0].Name = "Renamed electric";
        var saved = await db.Service().SaveDraftAsync(draft.Id, new() { ExpectedEditVersion = draft.EditVersion, EffectiveFromMonth = draft.EffectiveFromMonth, SetupStep = BudgetSetupStep.Review, IncomeSources = draft.IncomeSources.ToList(), Allocations = draft.Allocations.ToList() });
        await db.Service().PublishDraftAsync(saved.Id, saved.EditVersion);
        var bill = Assert.Single((await db.Service().GetMonthAsync(new(2026,10,1))).Events, e => !e.IsIncome);
        Assert.Equal("Renamed electric", bill.Name); Assert.True(bill.Payment!.IsPaid);
    }
}
