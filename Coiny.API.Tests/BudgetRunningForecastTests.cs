using Coiny.API.Enums;
using Coiny.API.DTOs.Budgets;
using Coiny.API.Interfaces;
using Coiny.API.Services;
using Microsoft.EntityFrameworkCore;

namespace Coiny.API.Tests;

public class BudgetRunningForecastTests
{
    private sealed class User(int household=1) : ICurrentUserService
    {
        public bool IsAuthenticated()=>true; public string? GetUserId()=>"test";
        public Task<int> GetCurrentHouseholdIdAsync()=>Task.FromResult(household);
    }
    private static BudgetActualsService Service(BudgetTestDatabase db,int household=1)=>new(db.Context(),new User(household),db.Service(household),db.Clock);
    private static async Task<Dictionary<string,Guid>> Plan(BudgetTestDatabase db,decimal salary=1000,decimal savings=0,decimal investments=0)
    {
        db.Clock.Now=new(2026,10,1,16,0,0,TimeSpan.Zero);
        var budgets=db.Service(); var draft=await budgets.StartDraftAsync();
        var allocations=new List<BudgetAllocationInput> {
            new() { Name="First bill",Amount=500,AllocationType=BudgetAllocationType.Bills,Schedule=new() { Frequency=BudgetFrequency.Monthly,StartDate=new(2026,10,1),DayOfMonth=5 } },
            new() { Name="Second bill",Amount=1200,AllocationType=BudgetAllocationType.Bills,Schedule=new() { Frequency=BudgetFrequency.Monthly,StartDate=new(2026,10,1),DayOfMonth=20 } }
        };
        foreach (var (name,amount,type) in new[] { ("Savings",savings,BudgetAllocationType.Savings),("Investments",investments,BudgetAllocationType.Investments) })
            if (amount>0) allocations.Add(new() { Name=name,Amount=amount,AllocationType=type,Schedule=new() { Frequency=BudgetFrequency.Monthly,StartDate=new(2026,10,1),DayOfMonth=10 } });
        var saved=await budgets.SaveDraftAsync(draft.Id,new() {
            ExpectedEditVersion=draft.EditVersion,EffectiveFromMonth=new(2026,10,1),SetupStep=BudgetSetupStep.Review,
            IncomeSources=[new() { Name="Salary",Amount=salary,Schedule=new() { Frequency=BudgetFrequency.EveryTwoWeeks,StartDate=new(2026,10,2) } }],
            Allocations=allocations
        });
        await budgets.PublishDraftAsync(saved.Id,saved.EditVersion);
        return saved.Allocations.ToDictionary(a=>a.Name,a=>a.SourceKey!.Value);
    }
    [Theory]
    [InlineData(4800,800,0,3800,0)]
    [InlineData(3800,0,200,2800,0)]
    [InlineData(500,0,3500,0,500)]
    public async Task Savings_estimate_combines_forecast_contributions_and_remainder_while_separating_investments(decimal income,decimal extra,decimal shortfall,decimal potential,decimal beyond)
    {
        await using var db=await BudgetTestDatabase.CreateAsync(); await Plan(db,income,3000,500);
        var forecast=await Service(db).GetForecastAsync(new(2026,10,2),0);
        Assert.Equal(3000,forecast.PlannedSavingsContributions); Assert.Equal(3000,forecast.ForecastSavingsContributions);
        Assert.Equal(500,forecast.PlannedInvestmentContributions); Assert.Equal(500,forecast.ForecastInvestmentContributions);
        Assert.Equal(extra,forecast.AdditionalSavings); Assert.Equal(shortfall,forecast.FundingShortfall);
        Assert.Equal(potential,forecast.PotentialSavings); Assert.Equal(beyond,forecast.ShortfallBeyondSavings);
        Assert.Equal(Math.Max(0,forecast.ForecastSavingsContributions+forecast.Points[^1].Forecast!.Value),forecast.PotentialSavings);
    }
    private static async Task Ledger(BudgetTestDatabase db)
    {
        var c=db.Context(); c.Accounts.Add(new() { Id=1,HouseholdId=1,Name="Checking",OpeningBalance=99999,OpeningBalanceDate=new(2026,10,1) });
        c.Accounts.Add(new() { Id=2,HouseholdId=2,Name="Other",OpeningBalanceDate=new(2026,10,1) });
        c.Categories.AddRange(new Models.Category { Id=1,HouseholdId=1,Name="Bills",CategoryType=CategoryType.Expense },
            new Models.Category { Id=2,HouseholdId=1,Name="Pay",CategoryType=CategoryType.Income },
            new Models.Category { Id=3,HouseholdId=1,Name="Transfer",CategoryType=CategoryType.Transfer },
            new Models.Category { Id=4,HouseholdId=2,Name="Foreign",CategoryType=CategoryType.Income });
        c.Transactions.AddRange(new Models.Transaction { Id=1,HouseholdId=1,AccountId=1,CategoryId=2,Description="Salary",Amount=1000,TransactionDate=new(2026,10,2) },
            new Models.Transaction { Id=2,HouseholdId=1,AccountId=1,CategoryId=1,Description="First bill",Amount=550,TransactionDate=new(2026,10,5) },
            new Models.Transaction { Id=3,HouseholdId=1,AccountId=1,CategoryId=3,Description="Transfer",Amount=700,TransactionDate=new(2026,10,8) },
            new Models.Transaction { Id=4,HouseholdId=2,AccountId=2,CategoryId=4,Description="Foreign income",Amount=9999,TransactionDate=new(2026,10,3) });
        await c.SaveChangesAsync();
    }
    [Fact] public async Task Potential_savings_uses_forecast_contributions_instead_of_adding_an_outdated_goal_to_remaining_cash()
    {
        await using var db=await BudgetTestDatabase.CreateAsync(); var keys=await Plan(db,4800,3000,500); await Ledger(db);
        db.Clock.Now=new(2026,10,11,16,0,0,TimeSpan.Zero);
        var c=db.Context(); (await c.Transactions.SingleAsync(t=>t.Id==1)).Amount=4800;
        c.Transactions.Add(new() { Id=5,HouseholdId=1,AccountId=1,CategoryId=1,Description="Savings contribution",Amount=2800,TransactionDate=new(2026,10,10) }); await c.SaveChangesAsync();
        var payments=new BillPaymentService(db.Context(),new User(),db.Service(),db.Clock);
        await payments.SaveAsync(keys["First bill"],new(2026,10,5),new() { IsPaid=true,TransactionId=2 });
        await payments.SaveAsync(keys["Savings"],new(2026,10,10),new() { IsPaid=true,TransactionId=5 });
        var forecast=await Service(db).GetForecastAsync(new(2026,10,11),0);
        Assert.Equal(3000,forecast.PlannedSavingsContributions); Assert.Equal(2800,forecast.ForecastSavingsContributions);
        Assert.Equal(950,forecast.AdditionalSavings); Assert.Equal(3750,forecast.PotentialSavings);
        Assert.Equal(500,forecast.ForecastInvestmentContributions);
    }
    [Fact] public async Task Surpluses_and_shortfalls_carry_forward_across_complete_periods_and_future_ranges_have_no_actuals()
    {
        await using var db=await BudgetTestDatabase.CreateAsync(); await Plan(db);
        var forecast=await Service(db).GetForecastAsync(new(2026,10,2),1);
        Assert.Equal(500,forecast.Periods[0].ForecastRemainder); Assert.Equal(-200,forecast.Periods[1].ForecastRemainder);
        Assert.Equal(300,forecast.Periods[1].CumulativeForecast);
        Assert.Equal(300,Assert.Single(forecast.Points,p=>p.Date==new DateOnly(2026,10,29)).Forecast);
        Assert.Equal(new DateOnly(2026,11,12),forecast.EndDate); Assert.Null(forecast.ThroughDate);
        Assert.All(forecast.Points,p=>Assert.Null(p.Actual));
        Assert.All(forecast.Points,p=>Assert.Equal(p.Planned,p.Forecast));
        var period=await Service(db).GetForecastAsync(new(2026,10,2),0); Assert.Single(period.Periods);
        var year=await Service(db).GetForecastAsync(new(2026,10,2),12); Assert.True(year.Periods.Count>20);
        Assert.Equal(year.Periods.Sum(p=>p.ForecastRemainder),year.Points[^1].Forecast);
        Assert.All(year.Periods.Skip(1),p=>Assert.Contains(year.Periods,previous=>previous.EndDate.AddDays(1)==p.StartDate));
        Assert.Empty(await db.Context().Transactions.ToListAsync());
    }
    [Fact] public async Task Recorded_income_and_allocations_anchor_forecast_without_including_balances_transfers_or_other_households()
    {
        await using var db=await BudgetTestDatabase.CreateAsync(); var keys=await Plan(db); await Ledger(db);
        db.Clock.Now=new(2026,10,10,16,0,0,TimeSpan.Zero);
        var payments=new BillPaymentService(db.Context(),new User(),db.Service(),db.Clock);
        await payments.SaveAsync(keys["First bill"],new(2026,10,5),new() { IsPaid=true,TransactionId=2 });
        var forecast=await Service(db).GetForecastAsync(new(2026,10,10),1);
        var anchor=Assert.Single(forecast.Points,p=>p.Date==new DateOnly(2026,10,10));
        Assert.Equal(450,anchor.Actual); Assert.Equal(anchor.Actual,anchor.Forecast);
        Assert.Equal(250,forecast.Periods[1].CumulativeForecast); Assert.Equal(0,forecast.ExpectedIncomeAwaitingTransaction);
        Assert.All(forecast.Points.Where(p=>p.Date>new DateOnly(2026,10,10)),p=>Assert.Null(p.Actual));
        Assert.All(forecast.Points.Where(p=>p.Date<new DateOnly(2026,10,10)),p=>Assert.Null(p.Forecast));
        var c=db.Context(); c.Transactions.Add(new() { Id=5,HouseholdId=1,AccountId=1,CategoryId=1,Description="Paid next bill early",Amount=1200,TransactionDate=new(2026,10,10) }); await c.SaveChangesAsync();
        await payments.SaveAsync(keys["Second bill"],new(2026,10,20),new() { IsPaid=true,TransactionId=5 });
        forecast=await Service(db).GetForecastAsync(new(2026,10,10),1);
        Assert.Equal(-750,Assert.Single(forecast.Points,p=>p.Date==new DateOnly(2026,10,10)).Actual);
        Assert.Equal(0,forecast.Periods[1].ForecastAllocations); Assert.Equal(250,forecast.Periods[1].CumulativeForecast);
    }
    [Fact] public async Task Pending_income_is_explicit_and_queued_budget_changes_apply_to_future_periods()
    {
        await using var db=await BudgetTestDatabase.CreateAsync(); var keys=await Plan(db);
        db.Clock.Now=new(2026,10,10,16,0,0,TimeSpan.Zero);
        var forecast=await Service(db).GetForecastAsync(new(2026,10,10),1);
        Assert.Equal(1000,forecast.ExpectedIncomeAwaitingTransaction);
        Assert.Equal(0,Assert.Single(forecast.Points,p=>p.Date==new DateOnly(2026,10,10)).Actual);
        Assert.Equal(500,forecast.Periods[0].ForecastRemainder);
        var management=await db.Service().GetManagementAsync();
        await db.Service().ChangeItemAsync(new() { ExpectedRevisionId=management.Revision.Id,SourceKey=keys["Second bill"],EffectiveDate=new(2026,10,16),
            Allocation=new() { Name="Second bill",Amount=1000,AllocationType=BudgetAllocationType.Bills,Schedule=management.Revision.Allocations.Single(a=>a.Name=="Second bill").Schedule } },false);
        forecast=await Service(db).GetForecastAsync(new(2026,10,10),1);
        Assert.Equal(0,forecast.Periods[1].ForecastRemainder); Assert.Equal(500,forecast.Periods[1].CumulativeForecast);
        await Assert.ThrowsAsync<ArgumentException>(()=>Service(db).GetForecastAsync(new(2026,10,10),2));
        await Assert.ThrowsAsync<ArgumentException>(()=>Service(db,2).GetForecastAsync(new(2026,10,10),1));
    }
    [Fact] public async Task Transfer_paid_early_for_a_future_budget_item_counts_once_in_the_posting_period()
    {
        await using var db=await BudgetTestDatabase.CreateAsync(); var keys=await Plan(db); await Ledger(db);
        db.Clock.Now=new(2026,10,10,16,0,0,TimeSpan.Zero);
        var payments=new BillPaymentService(db.Context(),new User(),db.Service(),db.Clock);
        await payments.SaveAsync(keys["First bill"],new(2026,10,5),new() { IsPaid=true,TransactionId=2 });
        var management=await db.Service().GetManagementAsync();
        await db.Service().ChangeItemAsync(new() { ExpectedRevisionId=management.Revision.Id,EffectiveDate=new(2026,10,16),
            Allocation=new() { Name="New IRA",Amount=200,AllocationType=BudgetAllocationType.Investments,Schedule=new() { Frequency=BudgetFrequency.Monthly,StartDate=new(2026,10,16),DayOfMonth=20 } } },false);
        var key=(await db.Service().GetManagementAsync()).Revision.Allocations.Single(a=>a.Name=="New IRA").SourceKey!.Value;
        var c=db.Context(); c.Transactions.Add(new() { Id=5,HouseholdId=1,AccountId=1,CategoryId=3,Description="Early contribution",Amount=200,TransactionDate=new(2026,10,10) }); await c.SaveChangesAsync();
        await payments.SaveAsync(key,new(2026,10,20),new() { IsPaid=true,TransactionId=5 });
        var actuals=await Service(db).GetPeriodAsync(new(2026,10,10),"all");
        Assert.Equal(200,actuals.Points[8].ActualContributions);
        Assert.Equal(200,Assert.Single(actuals.Items,i=>i.AllocationKey==key).FuturePeriodActual);
        var forecast=await Service(db).GetForecastAsync(new(2026,10,10),1);
        Assert.Equal(250,Assert.Single(forecast.Points,p=>p.Date==new DateOnly(2026,10,10)).Actual);
        Assert.Equal(1200,forecast.Periods[1].ForecastAllocations); // Bill only; contribution already posted.
        Assert.Equal(50,forecast.Periods[1].CumulativeForecast);
        Assert.Equal(200,forecast.ForecastInvestmentContributions); // Early contribution appears once across both periods.
        // If the other bank posts the receiving side later, actuals and forecast both use
        // that receiving date rather than dropping the contribution between the two periods.
        c.Accounts.Add(new() { Id=3,HouseholdId=1,Name="Investment",AccountType=AccountType.Investment,OpeningBalanceDate=new(2026,10,1) });
        c.Transactions.Add(new() { Id=6,HouseholdId=1,AccountId=3,CategoryId=3,Description="Contribution received",Amount=200,TransactionDate=new(2026,10,16),TransferDirection=TransferDirection.In,TransferCounterparty=TransferCounterparty.Linked,PairedTransactionId=5 }); await c.SaveChangesAsync();
        var outgoing=await c.Transactions.SingleAsync(t=>t.Id==5); outgoing.TransferDirection=TransferDirection.Out; outgoing.TransferCounterparty=TransferCounterparty.Linked; outgoing.PairedTransactionId=6; await c.SaveChangesAsync();
        forecast=await Service(db).GetForecastAsync(new(2026,10,10),1);
        Assert.Equal(450,Assert.Single(forecast.Points,p=>p.Date==new DateOnly(2026,10,10)).Actual);
        Assert.Equal(1400,forecast.Periods[1].ForecastAllocations);
        Assert.Equal(50,forecast.Periods[1].CumulativeForecast);
        Assert.Equal(200,forecast.ForecastInvestmentContributions);
    }
}
