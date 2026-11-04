using Coiny.API.DTOs.Budgets;
using Coiny.API.Enums;
using Coiny.API.Interfaces;
using Coiny.API.Models;
using Coiny.API.Services;
using Microsoft.EntityFrameworkCore;
namespace Coiny.API.Tests;

public class BillOccurrenceSkipTests
{
    private static readonly DateOnly Due = new(2026,10,17);
    private sealed class User(int household=1):ICurrentUserService {
        public string? GetUserId()=>"test";public bool IsAuthenticated()=>true;
        public Task<int> GetCurrentHouseholdIdAsync()=>Task.FromResult(household);
    }
    private static BillPaymentService Payments(BudgetTestDatabase db,int household=1)=>new(db.Context(),new User(household),db.Service(household),db.Clock);
    private static BudgetActualsService Actuals(BudgetTestDatabase db)=>new(db.Context(),new User(),db.Service(),db.Clock);
    private static async Task<Guid> Plan(BudgetTestDatabase db,BudgetAllocationType type=BudgetAllocationType.Bills){
        var service=db.Service();var draft=await service.StartDraftAsync();
        var saved=await service.SaveDraftAsync(draft.Id,new(){ExpectedEditVersion=draft.EditVersion,EffectiveFromMonth=new(2026,10,1),SetupStep=BudgetSetupStep.Review,
            IncomeSources=[new(){Name="Salary",Amount=2000,Schedule=new(){Frequency=BudgetFrequency.EveryTwoWeeks,StartDate=new(2026,10,16)}}],
            Allocations=[new(){Name="Babysitter",Amount=100,AllocationType=type,Schedule=new(){Frequency=BudgetFrequency.Weekly,StartDate=Due}}]});
        await service.PublishDraftAsync(saved.Id,saved.EditVersion);
        var c=db.Context();c.Accounts.Add(new(){Id=1,HouseholdId=1,Name="Checking",AccountType=AccountType.Checking,OpeningBalance=500,OpeningBalanceDate=new(2026,10,1)});await c.SaveChangesAsync();
        return saved.Allocations[0].SourceKey!.Value;
    }
    [Theory]
    [InlineData(BudgetAllocationType.Bills)] [InlineData(BudgetAllocationType.Housing)] [InlineData(BudgetAllocationType.Subscriptions)]
    public async Task One_occurrence_releases_commitment_and_forecast_preserves_target_and_restores(BudgetAllocationType type){
        await using var db=await BudgetTestDatabase.CreateAsync();var key=await Plan(db,type);
        var before=await db.Service().GetIncomePeriodAsync();Assert.Equal(200,before.UnpaidBillsTotal);
        var monthBefore=await Actuals(db).GetAsync(new(2026,10,1));var reportBefore=await Actuals(db).GetPeriodAsync();
        var cashBefore=await Actuals(db).GetAvailableCashAsync(new());
        var skipped=await Payments(db).SaveAsync(key,Due,new(){IsSkipped=true,Note="  Sitter away  "});Assert.Equal("Skipped",skipped.Status);Assert.False(skipped.IsPaid);Assert.Null(skipped.TransactionId);Assert.Null(skipped.PaidOn);
        var period=await db.Service().GetIncomePeriodAsync();Assert.Equal(100,period.UnpaidBillsTotal);Assert.Equal(before.BillsTotal,period.BillsTotal);Assert.Equal(before.Plan!.Allocated,period.Plan!.Allocated);
        Assert.True(period.Bills.Single(b=>b.Date==Due).Payment!.IsSkipped);Assert.Null(period.Bills.Single(b=>b.Date==Due.AddDays(7)).Payment);
        var report=await Actuals(db).GetPeriodAsync();Assert.Equal(reportBefore.Points[^1].ForecastSpending-100,report.Points[^1].ForecastSpending);Assert.Equal(reportBefore.Planned,report.Planned);
        var month=await Actuals(db).GetAsync(new(2026,10,1));Assert.Equal(monthBefore.Items.Single().ForecastRemaining-100,month.Items.Single().ForecastRemaining);Assert.Equal(monthBefore.Planned,month.Planned);
        var cash=await Actuals(db).GetAvailableCashAsync(new());Assert.Equal(cashBefore.RemainingAllocations-100,cash.RemainingAllocations);
        await Payments(db).SaveAsync(key,Due,new(){IsSkipped=true,Note="Sitter away"});Assert.Single(await db.Context().BillPayments.ToListAsync());
        var row=await db.Context().BillPayments.SingleAsync();Assert.Null(row.SkippedPeriodStart);Assert.Null(row.SkippedPeriodEnd);
        Assert.Equal("Sitter away",row.Note);Assert.Empty(await db.Context().Transactions.ToListAsync());Assert.Empty(await db.Context().AccountBalanceSnapshots.ToListAsync());
        await Payments(db).SaveAsync(key,Due,new(){IsSkipped=false});Assert.Equal(before.UnpaidBillsTotal,(await db.Service().GetIncomePeriodAsync()).UnpaidBillsTotal);
        Assert.Equal(reportBefore.Points[^1].ForecastSpending,(await Actuals(db).GetPeriodAsync()).Points[^1].ForecastSpending);
    }
    [Fact] public async Task Cross_month_skip_never_becomes_a_savings_period_or_skips_another_week(){
        await using var db=await BudgetTestDatabase.CreateAsync();var key=await Plan(db);
        var before=await db.Service().GetIncomePeriodAsync(new(2026,10,30));await Payments(db).SaveAsync(key,new(2026,10,31),new(){IsSkipped=true});
        var period=await db.Service().GetIncomePeriodAsync(new(2026,10,30));Assert.Equal(before.UnpaidBillsTotal-100,period.UnpaidBillsTotal);Assert.Empty(period.Contributions);
        Assert.Null(period.Bills.Single(b=>b.Date==new DateOnly(2026,11,7)).Payment);Assert.Equal(200,(await db.Service().GetIncomePeriodAsync(new(2026,10,16))).UnpaidBillsTotal);
        Assert.DoesNotContain((await db.Service().GetMonthAsync(new(2026,11,1))).Events,e=>e.Payment?.IsSkipped==true);
    }
    [Fact] public async Task Linked_or_manually_paid_occurrences_cannot_be_silently_skipped_and_actuals_remain(){
        await using var db=await BudgetTestDatabase.CreateAsync();var key=await Plan(db);var c=db.Context();
        c.Categories.Add(new(){Id=1,HouseholdId=1,Name="Childcare",CategoryType=CategoryType.Expense});c.Transactions.Add(new(){Id=1,HouseholdId=1,AccountId=1,CategoryId=1,Amount=75,TransactionDate=new(2026,10,16),Description="Babysitter"});await c.SaveChangesAsync();
        await Payments(db).SaveAsync(key,Due,new(){IsPaid=true,TransactionIds=[1]});
        await Assert.ThrowsAsync<ArgumentException>(()=>Payments(db).SaveAsync(key,Due,new(){IsSkipped=true}));Assert.Equal(1,(await db.Context().BillPayments.SingleAsync()).TransactionId);
        await Payments(db).SaveAsync(key,Due,new(){IsPaid=true,TransactionIds=[]});await Assert.ThrowsAsync<ArgumentException>(()=>Payments(db).SaveAsync(key,Due,new(){IsSkipped=true}));
        await Payments(db).SaveAsync(key,Due,new(){IsPaid=false,TransactionIds=[]});await Payments(db).SaveAsync(key,Due,new(){IsSkipped=true});
        Assert.Equal(75,(await db.Context().Transactions.SingleAsync()).Amount);var report=await Actuals(db).GetPeriodAsync();Assert.Equal(75,report.UnassignedExpenses);Assert.Equal(75,report.Points.Single(p=>p.Date==new DateOnly(2026,10,16)).ActualSpending);
    }
    [Fact] public async Task Validation_and_household_scope_remain_enforced(){
        await using var db=await BudgetTestDatabase.CreateAsync();var key=await Plan(db,BudgetAllocationType.Investments);
        await Assert.ThrowsAsync<ArgumentException>(()=>Payments(db).SaveAsync(key,Due,new(){IsSkipped=true}));
        await Assert.ThrowsAsync<ArgumentException>(()=>Payments(db,2).SaveAsync(key,Due,new(){IsSkipped=true}));
        await Assert.ThrowsAsync<ArgumentException>(()=>Payments(db).SaveAsync(key,Due.AddDays(1),new(){IsSkipped=true}));Assert.Empty(await db.Context().BillPayments.ToListAsync());
    }
}
