using Coiny.API.DTOs.Budgets;
using Coiny.API.Enums;
using Coiny.API.Interfaces;
using Coiny.API.Services;
using Microsoft.EntityFrameworkCore;
namespace Coiny.API.Tests;

public class BudgetPeriodProjectionTests
{
    private sealed class User : ICurrentUserService {
        public bool IsAuthenticated() => true; public string? GetUserId() => "test";
        public Task<int> GetCurrentHouseholdIdAsync() => Task.FromResult(1);
    }
    private static async Task<BudgetRevisionResponse> Publish(BudgetTestDatabase db, bool staggered = false)
    {
        db.Clock.Now = new(2026,9,1,16,0,0,TimeSpan.Zero);
        var service = db.Service(); var draft = await service.StartDraftAsync(new(2026,9,1));
        var saved = await service.SaveDraftAsync(draft.Id,new() {
            ExpectedEditVersion = draft.EditVersion, EffectiveFromMonth = new(2026,9,1), SetupStep = BudgetSetupStep.Review,
            IncomeSources = [new() { Name = "Alex", Amount = 3000, Schedule = new() { Frequency = BudgetFrequency.EveryTwoWeeks, StartDate = new(2026,9,18) } },
                new() { Name = "Hannah", Amount = 2000, Schedule = new() { Frequency = staggered ? BudgetFrequency.Weekly : BudgetFrequency.EveryTwoWeeks, StartDate = staggered ? new(2026,9,23) : new(2026,9,18) } }],
            Allocations = [new() { Name = "Car goal", Amount = 500, AllocationType = BudgetAllocationType.Savings },
                new() { Name = "Groceries", Amount = 500, AllocationType = BudgetAllocationType.Spending },
                new() { Name = "Electric", Amount = 100, AllocationType = BudgetAllocationType.Bills, Schedule = new() { Frequency = BudgetFrequency.Monthly, StartDate = new(2026,9,1), DayOfMonth = 5 } },
                new() { Name = "IRA", Amount = 75, AllocationType = BudgetAllocationType.Investments, Schedule = new() { Frequency = BudgetFrequency.Monthly, StartDate = new(2026,9,1), DayOfMonth = 1 } }]
        });
        await service.PublishDraftAsync(saved.Id,saved.EditVersion);
        db.Clock.Now = new(2026,10,16,16,0,0,TimeSpan.Zero); return saved;
    }
    [Fact] public async Task October_goal_shares_preserve_cents_and_cross_month_funding_with_payday_income()
    {
        await using var db = await BudgetTestDatabase.CreateAsync(); await Publish(db);
        var first = await db.Service().GetIncomePeriodAsync(new(2026,10,2));
        var second = await db.Service().GetIncomePeriodAsync(new(2026,10,16));
        var third = await db.Service().GetIncomePeriodAsync(new(2026,10,30));
        Assert.Equal(5000,first.Plan!.ExpectedIncome); Assert.Equal(225.81m,Assert.Single(first.Contributions,e => e.IsPeriodSetAside).Amount);
        Assert.Equal(225.80m,Assert.Single(second.Contributions,e => e.IsPeriodSetAside).Amount);
        var lastGoal = Assert.Single(third.Contributions,e => e.IsPeriodSetAside);
        Assert.Equal(new DateOnly(2026,11,12),lastGoal.Date); Assert.Equal(232.26m,lastGoal.Amount);
        Assert.Equal(75,Assert.Single(third.Contributions,e => !e.IsPeriodSetAside).Amount);
        Assert.Equal(500,225.81m+225.80m+32.26m+16.13m);
        Assert.Equal(639.52m,third.Plan!.Allocated); Assert.Equal(4360.48m,third.ExpectedRemaining);
        foreach (var period in new[] { first,second,third }) {
            Assert.Equal(period.Plan!.Allocated,period.PlannedPoints[^1].PlannedSpending+period.PlannedPoints[^1].PlannedContributions);
            Assert.Equal(period.Plan.Allocations.Sum(a => a.PlannedAmount),period.Plan.Allocated);
        }
        Assert.Empty(await db.Context().BillPayments.ToListAsync()); Assert.Empty(await db.Context().Transactions.ToListAsync());
    }
    [Fact] public async Task Mixed_income_schedules_share_one_goal_timeline_and_individual_views_do_not_duplicate_it()
    {
        await using var db = await BudgetTestDatabase.CreateAsync(); var saved = await Publish(db,true);
        var individual = await db.Service().GetIncomePeriodAsync(new(2026,10,2),saved.IncomeSources[0].SourceKey);
        var goals = individual.Contributions.Where(e => e.IsPeriodSetAside).ToList();
        Assert.Equal(3,goals.Count); Assert.Equal(225.81m,goals.Sum(e => e.Amount));
        foreach (var goal in goals) {
            var household = await db.Service().GetIncomePeriodAsync(goal.Date);
            Assert.Equal(goal.Amount,Assert.Single(household.Contributions,e => e.IsPeriodSetAside).Amount);
        }
    }
    [Fact] public async Task Goal_can_link_to_transaction_and_survives_publication_without_moving_money()
    {
        await using var db = await BudgetTestDatabase.CreateAsync(); await Publish(db);
        var period = await db.Service().GetIncomePeriodAsync(new(2026,10,16)); var goal = Assert.Single(period.Contributions,e => e.IsPeriodSetAside);
        var c = db.Context(); c.Accounts.Add(new() { Id=1, HouseholdId=1, Name="Checking",OpeningBalanceDate=new(2026,9,1) });
        c.Categories.Add(new() { Id=1,HouseholdId=1,Name="Car savings",CategoryType=CategoryType.Expense });
        c.Transactions.Add(new() { Id=1,HouseholdId=1,AccountId=1,CategoryId=1,Description="Car set aside",Amount=goal.Amount,TransactionDate=new(2026,10,16) }); await c.SaveChangesAsync();
        var payments = new BillPaymentService(db.Context(),new User(),db.Service(),db.Clock);
        await payments.SaveAsync(goal.SourceKey!.Value,goal.Date,new() { IsPaid=true,TransactionId=1 });
        Assert.Equal(0,(await db.Service().GetIncomePeriodAsync(new(2026,10,16))).UnpaidContributionsTotal);
        var draft = await db.Service().StartDraftAsync();
        draft = await db.Service().SaveDraftAsync(draft.Id,new() { ExpectedEditVersion=draft.EditVersion,EffectiveFromMonth=draft.EffectiveFromMonth,SetupStep=BudgetSetupStep.Review,IncomeSources=draft.IncomeSources.ToList(),Allocations=draft.Allocations.ToList() });
        await db.Service().PublishDraftAsync(draft.Id,draft.EditVersion);
        Assert.Equal(1,(await db.Context().BillPayments.SingleAsync()).TransactionId);
        Assert.Equal(1,await db.Context().Transactions.CountAsync());
        await Assert.ThrowsAsync<ArgumentException>(() => payments.SaveAsync(goal.SourceKey.Value,goal.Date.AddDays(-1),new() { IsPaid=true }));
    }
    [Fact] public async Task Period_actuals_use_posting_ranges_and_each_transactions_effective_month_rule()
    {
        await using var db = await BudgetTestDatabase.CreateAsync(); var saved = await Publish(db);
        db.Clock.Now=new(2026,11,15,16,0,0,TimeSpan.Zero);
        var c=db.Context(); c.Accounts.Add(new() { Id=1,HouseholdId=1,Name="Checking",OpeningBalanceDate=new(2026,9,1) });
        c.Categories.Add(new() { Id=1,HouseholdId=1,Name="Car",CategoryType=CategoryType.Expense });
        foreach (var (id,date) in new[] { (1,new DateTime(2026,10,29)),(2,new DateTime(2026,10,31)),(3,new DateTime(2026,11,1)),(4,new DateTime(2026,11,13)) })
            c.Transactions.Add(new() { Id=id,HouseholdId=1,AccountId=1,CategoryId=1,Description="Expense",Amount=id*10,TransactionDate=date });
        await c.SaveChangesAsync();
        var actuals=new BudgetActualsService(db.Context(),new User(),db.Service(),db.Clock);
        await actuals.SaveCategoryAsync(1,new() { Month=new(2026,10,1),AllocationKey=saved.Allocations[0].SourceKey });
        await actuals.SaveCategoryAsync(1,new() { Month=new(2026,11,1),AllocationKey=saved.Allocations[1].SourceKey });
        var report=await actuals.GetPeriodAsync(new(2026,10,30),"all");
        Assert.Equal(2,report.TotalCount); Assert.Equal(50,report.AssignedActual);
        Assert.Equal(20,Assert.Single(report.Items,i=>i.Name=="Car goal").Actual);
        Assert.Equal(30,Assert.Single(report.Items,i=>i.Name=="Groceries").Actual);
        var detail=await actuals.GetPeriodAsync(new(2026,10,30),allocationKey:saved.Allocations[0].SourceKey);
        Assert.Equal(2,Assert.Single(detail.Transactions).Id);
        Assert.Equal(14,report.Points.Count); Assert.Equal(report.Planned,report.Points[^1].PlannedSpending+report.Points[^1].PlannedContributions);
    }
}
