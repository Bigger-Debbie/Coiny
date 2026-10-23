using Coiny.API.DTOs.Budgets;
using Coiny.API.Enums;
using Coiny.API.Models;
using Microsoft.EntityFrameworkCore;

namespace Coiny.API.Tests;

public class BudgetManagementTests
{
    private static async Task<BudgetManagementResponse> Plan(BudgetTestDatabase db)
    {
        var service = db.Service(); var draft = await service.StartDraftAsync();
        var saved = await service.SaveDraftAsync(draft.Id, new() {
            ExpectedEditVersion = draft.EditVersion, EffectiveFromMonth = new(2026,10,1), SetupStep = BudgetSetupStep.Review,
            IncomeSources = [new() { Name = "Salary", Amount = 2000, Schedule = new() { Frequency = BudgetFrequency.EveryTwoWeeks, StartDate = new(2026,10,2) } }],
            Allocations = [new() { Name = "Electric", Amount = 100, AllocationType = BudgetAllocationType.Bills, Schedule = new() { Frequency = BudgetFrequency.Monthly, StartDate = new(2026,10,1), DayOfMonth = 25 } },
                new() { Name = "Groceries", Amount = 310, AllocationType = BudgetAllocationType.Spending },
                new() { Name = "Car fund", Amount = 310, AllocationType = BudgetAllocationType.Savings }],
        });
        await service.PublishDraftAsync(saved.Id, saved.EditVersion);
        return await service.GetManagementAsync();
    }
    private static BudgetItemChange Change(BudgetManagementResponse plan, string item, decimal amount) => new() {
        ExpectedRevisionId = plan.Revision.Id, EffectiveDate = new(2026,10,16),
        SourceKey = plan.Revision.Allocations.Single(a => a.Name == item).SourceKey,
        Allocation = new() { Name = item, Amount = amount, AllocationType = plan.Revision.Allocations.Single(a => a.Name == item).AllocationType,
            Schedule = plan.Revision.Allocations.Single(a => a.Name == item).Schedule },
    };
    [Fact]
    public async Task Midmonth_edits_prorate_only_remaining_days_and_preview_does_not_publish()
    {
        await using var db = await BudgetTestDatabase.CreateAsync(); var plan = await Plan(db); var service = db.Service();
        var change = Change(plan, "Groceries", 620);
        var review = await service.ChangeItemAsync(change, true);
        Assert.Equal(470, review.After.Allocations.Single(a => a.Name == "Groceries").PlannedAmount);
        Assert.Single(await db.Context().BudgetRevisions.ToListAsync());
        Assert.Equal(310, (await service.GetMonthAsync(new(2026,10,1))).Allocations.Single(a => a.Name == "Groceries").PlannedAmount);
        await service.ChangeItemAsync(change, false);
        var past = await service.GetIncomePeriodAsync(new(2026,10,10));
        Assert.Equal(140, past.Plan!.Allocations.Single(a => a.Name == "Groceries").PlannedAmount);
        var current = await service.GetIncomePeriodAsync(new(2026,10,20));
        Assert.Equal(280, current.Plan!.Allocations.Single(a => a.Name == "Groceries").PlannedAmount);
        var monthly = await service.GetMonthAsync(new(2026,10,1));
        Assert.Equal(monthly.Allocated, monthly.PlannedPoints!.Last().PlannedSpending + monthly.PlannedPoints!.Last().PlannedContributions);
        Assert.Equal(current.Plan.Allocated, current.PlannedPoints.Last().PlannedSpending + current.PlannedPoints.Last().PlannedContributions);
    }
    [Fact]
    public async Task Removal_and_schedule_change_preserve_paid_future_occurrence_and_transaction_link()
    {
        await using var db = await BudgetTestDatabase.CreateAsync(); var plan = await Plan(db); var c = db.Context();
        var key = plan.Revision.Allocations.Single(a => a.Name == "Electric").SourceKey!.Value;
        c.Accounts.Add(new() { Id = 1, HouseholdId = 1, Name = "Spending", OpeningBalanceDate = new(2026,10,1) });
        c.Categories.Add(new() { Id = 1, HouseholdId = 1, Name = "Bills", CategoryType = CategoryType.Expense });
        c.Transactions.Add(new() { Id = 1, HouseholdId = 1, AccountId = 1, CategoryId = 1, Amount = 100, TransactionDate = new(2026,10,15), Description = "Paid early" });
        c.BillPayments.Add(new() { HouseholdId = 1, AllocationKey = key, DueDate = new(2026,10,25), IsPaid = true, TransactionId = 1, PaidOn = new(2026,10,15) });
        await c.SaveChangesAsync();
        var change = Change(plan, "Electric", 200); change.Allocation!.Schedule!.DayOfMonth = 26;
        await db.Service().ChangeItemAsync(change, false);
        var month = await db.Service().GetMonthAsync(new(2026,10,1));
        var paid = Assert.Single(month.Events, e => e.SourceKey == key);
        Assert.Equal(new DateOnly(2026,10,25), paid.Date); Assert.Equal(100, paid.Amount); Assert.Equal(1, paid.Payment!.TransactionId);
        var latest = await db.Service().GetManagementAsync(); var remove = Change(latest,"Electric",0); remove.Remove = true;
        await db.Service().ChangeItemAsync(remove,false);
        Assert.Equal(1, Assert.Single((await db.Service().GetMonthAsync(new(2026,10,1))).Events,e => e.SourceKey == key).Payment!.TransactionId);
        Assert.DoesNotContain((await db.Service().GetMonthAsync(new(2026,11,1))).Events,e => e.SourceKey == key);
        Assert.Single(await db.Context().Transactions.ToListAsync());
    }
    [Fact]
    public async Task Paid_period_set_aside_keeps_its_amount_and_monthly_totals_when_rate_changes()
    {
        await using var db = await BudgetTestDatabase.CreateAsync(); var plan = await Plan(db);
        var key = plan.Revision.Allocations.Single(a => a.Name == "Car fund").SourceKey!.Value;
        var c = db.Context(); c.BillPayments.Add(new() { HouseholdId = 1, AllocationKey = key, DueDate = new(2026,10,29), IsPaid = true, PaidOn = new(2026,10,16) }); await c.SaveChangesAsync();
        await db.Service().ChangeItemAsync(Change(plan,"Car fund",620),false);
        var period = await db.Service().GetIncomePeriodAsync(new(2026,10,20));
        var goal = Assert.Single(period.Contributions,e => e.SourceKey == key);
        Assert.Equal(140,goal.Amount); Assert.True(goal.Payment!.IsPaid);
        var month = await db.Service().GetMonthAsync(new(2026,10,1));
        Assert.Equal(330,month.Allocations.Single(a => a.SourceKey == key).PlannedAmount);
        Assert.Equal(month.Allocated,month.PlannedPoints!.Last().PlannedSpending + month.PlannedPoints!.Last().PlannedContributions);
        Assert.Equal(period.Plan!.Allocated,period.PlannedPoints.Last().PlannedSpending + period.PlannedPoints.Last().PlannedContributions);
    }
    [Fact]
    public async Task Paid_goal_is_not_duplicated_when_an_income_schedule_changes_period_boundaries()
    {
        await using var db = await BudgetTestDatabase.CreateAsync(); var plan = await Plan(db);
        var key = plan.Revision.Allocations.Single(a => a.Name == "Car fund").SourceKey!.Value;
        var c = db.Context(); c.BillPayments.Add(new() { HouseholdId = 1, AllocationKey = key, DueDate = new(2026,10,29), IsPaid = true, PaidOn = new(2026,10,16) }); await c.SaveChangesAsync();
        var source = plan.Revision.IncomeSources.Single(); source.Schedule.Frequency = BudgetFrequency.Weekly;
        await db.Service().ChangeItemAsync(new() { ExpectedRevisionId = plan.Revision.Id, EffectiveDate = new(2026,10,16), IsIncome = true, SourceKey = source.SourceKey, Income = source },false);
        var first = await db.Service().GetIncomePeriodAsync(new(2026,10,20));
        var second = await db.Service().GetIncomePeriodAsync(new(2026,10,25));
        Assert.DoesNotContain(first.Contributions,e => e.SourceKey == key);
        Assert.Equal(0,first.Plan!.Allocations.Single(a => a.SourceKey == key).PlannedAmount);
        Assert.Equal(140,Assert.Single(second.Contributions,e => e.SourceKey == key).Amount);
        Assert.Equal(second.Plan!.Allocated,second.PlannedPoints.Last().PlannedSpending + second.PlannedPoints.Last().PlannedContributions);
    }
    [Fact]
    public async Task Income_updates_and_new_items_start_on_the_effective_date_and_preserve_past_paydays()
    {
        await using var db = await BudgetTestDatabase.CreateAsync(); var plan = await Plan(db);
        var source = plan.Revision.IncomeSources.Single(); source.Amount = 2500;
        await db.Service().ChangeItemAsync(new() { ExpectedRevisionId = plan.Revision.Id, EffectiveDate = new(2026,10,23), IsIncome = true, SourceKey = source.SourceKey, Income = source },false);
        var month = await db.Service().GetMonthAsync(new(2026,10,1));
        Assert.Equal(new decimal[] { 2000,2000,2500 },month.Events.Where(e => e.IsIncome).Select(e => e.Amount));
        var latest = await db.Service().GetManagementAsync();
        await db.Service().ChangeItemAsync(new() { ExpectedRevisionId = latest.Revision.Id, EffectiveDate = new(2026,10,23), Allocation = new() { Name = "Streaming", Amount = 20, AllocationType = BudgetAllocationType.Subscriptions, Schedule = new() { Frequency = BudgetFrequency.Monthly,StartDate = new(2026,10,1),DayOfMonth = 24 } } },false);
        Assert.Equal(new DateOnly(2026,10,24),Assert.Single((await db.Service().GetMonthAsync(new(2026,10,1))).Events,e => e.Name == "Streaming").Date);
        Assert.DoesNotContain((await db.Service().GetIncomePeriodAsync(new(2026,10,10))).Bills,e => e.Name == "Streaming");
    }
    [Fact]
    public async Task Stale_foreign_past_dated_and_last_income_removal_are_rejected()
    {
        await using var db = await BudgetTestDatabase.CreateAsync(); var plan = await Plan(db);
        var change = Change(plan,"Electric",150); change.EffectiveDate = new(2026,10,15);
        await Assert.ThrowsAsync<ArgumentException>(() => db.Service().ChangeItemAsync(change,false));
        change.EffectiveDate = new(2026,10,16);
        await Assert.ThrowsAsync<ArgumentException>(() => db.Service(2).ChangeItemAsync(change,false));
        await db.Service().ChangeItemAsync(change,false);
        await Assert.ThrowsAsync<Coiny.API.Services.BudgetConflictException>(() => db.Service().ChangeItemAsync(change,false));
        var latest = await db.Service().GetManagementAsync();
        await Assert.ThrowsAsync<ArgumentException>(() => db.Service().ChangeItemAsync(new() { ExpectedRevisionId = latest.Revision.Id, EffectiveDate = new(2026,10,16), IsIncome = true, SourceKey = latest.Revision.IncomeSources[0].SourceKey, Remove = true },false));
    }
}
