using Coiny.API.DTOs.Budgets;
using Coiny.API.DTOs.Savings;
using Coiny.API.Enums;
using Coiny.API.Interfaces;
using Coiny.API.Models;
using Coiny.API.Services;
using Microsoft.EntityFrameworkCore;
namespace Coiny.API.Tests;
public class SavingsFundsTests
{
    private sealed class User(int household=1):ICurrentUserService { public string? GetUserId()=>"test";public bool IsAuthenticated()=>true;public Task<int> GetCurrentHouseholdIdAsync()=>Task.FromResult(household); }
    private static SavingsFundsService Service(BudgetTestDatabase db,int household=1){var c=db.Context();return new(c,new User(household),new BudgetService(c,new User(household),new BudgetScheduleService(),db.Clock),db.Clock);}
    private static async Task<BudgetRevisionResponse> Setup(BudgetTestDatabase db,bool publish=true)
    {
        var c=db.Context();c.Accounts.AddRange(new Account{Id=1,HouseholdId=1,Name="Savings",AccountType=AccountType.Savings,OpeningBalance=20000,OpeningBalanceDate=new(2026,1,1)},new Account{Id=2,HouseholdId=2,Name="Other",AccountType=AccountType.Savings,OpeningBalance=50000,OpeningBalanceDate=new(2026,1,1)},new Account{Id=3,HouseholdId=1,Name="Spending",OpeningBalanceDate=new(2026,1,1)});
        c.Categories.AddRange(new Category{Id=1,HouseholdId=1,Name="Expense",CategoryType=CategoryType.Expense},new Category{Id=2,HouseholdId=1,Name="Transfer",CategoryType=CategoryType.Transfer});await c.SaveChangesAsync();
        var service=db.Service();var d=await service.StartDraftAsync();d=await service.SaveDraftAsync(d.Id,new(){ExpectedEditVersion=d.EditVersion,EffectiveFromMonth=d.EffectiveFromMonth,SetupStep=BudgetSetupStep.Review,IncomeSources=[new(){Name="Pay",Amount=2000,Schedule=new(){Frequency=BudgetFrequency.EveryTwoWeeks,StartDate=new(2026,10,2)}}],Allocations=[new(){Name="Car",AllocationType=BudgetAllocationType.Savings,Amount=500},new(){Name="Food",AllocationType=BudgetAllocationType.Spending,Amount=600}]});
        return publish?await service.PublishDraftAsync(d.Id,d.EditVersion):d;
    }
    private static SaveSavingsFund Request(BudgetRevisionResponse revision,decimal opening=1000)
    {
        var component=new SavingsComponent{Name="Tires",Mode="Recurring",Amount=1000,TargetDate=new(2027,10,15),RepeatMonths=12};
        return new(){AcceptShortfall=true,SourceKey=revision.Allocations[0].SourceKey,AccountId=1,Name="Car",ExpectedRevisionId=revision.Id,Plan=new(){StartDate=new(2026,10,16),Components=[component]},OpeningAllocations=new(){{component.Key,opening}}};
    }
    [Fact] public async Task UnscheduledFundWorksWithoutBudgetAndKeepsLedgerAndAvailableCashIntact()
    {
        await using var db=await BudgetTestDatabase.CreateAsync();var context=db.Context();
        context.Accounts.Add(new Account{Id=1,HouseholdId=1,Name="Savings",AccountType=AccountType.Savings,OpeningBalance=1000,OpeningBalanceDate=new(2026,1,1)});await context.SaveChangesAsync();
        var component=new SavingsComponent{Name="Emergency",Mode="Monthly",Amount=0};
        var request=new SaveSavingsFund{AccountId=1,Name="Emergency fund",Plan=new(){ScheduleContributions=false,StartDate=new(2026,10,16),Components=[component]},OpeningAllocations=new(){{component.Key,600}}};
        var service=Service(db);Assert.Empty((await service.PreviewAsync(request)).Periods);
        var fund=await service.SaveAsync(request);Assert.Equal(600,fund.Balance);Assert.False(fund.Plan.ScheduleContributions);
        var account=Assert.Single(await service.GetAsync(1));Assert.Equal(1000,account.Balance);Assert.Equal(400,account.Unallocated);Assert.Null(Assert.Single(account.Funds).NextContributionDate);
        Assert.Empty(await context.BudgetRevisions.ToListAsync());Assert.Empty(await context.Transactions.ToListAsync());Assert.Empty(await context.BillPayments.ToListAsync());
        await Assert.ThrowsAsync<ArgumentException>(()=>Service(db).ActivityAsync(fund.SourceKey,new(){OperationId=Guid.NewGuid(),ExpectedEditVersion=fund.EditVersion,Date=new(2026,10,16),Kind="Allocate",Amounts=new(){{component.Key,401}}}));
        fund=await Service(db).ActivityAsync(fund.SourceKey,new(){OperationId=Guid.NewGuid(),ExpectedEditVersion=fund.EditVersion,Date=new(2026,10,16),Kind="Allocate",Amounts=new(){{component.Key,100}}});
        Assert.Equal(700,fund.Balance);
        fund=await Service(db).MoveAsync(fund.SourceKey,new(){OperationId=Guid.NewGuid(),ExpectedEditVersion=fund.EditVersion,ComponentKey=component.Key,Amount=700});
        await Service(db).ArchiveAsync(fund.SourceKey,new(fund.EditVersion,0,new(2026,10,16)));
        Assert.True(Assert.Single((await Service(db).GetAsync(1)).Single().Funds).IsArchived);
        Assert.Empty(await context.BudgetRevisions.ToListAsync());
    }
    [Fact] public async Task AdHocFundContributionsDoNotCreateBudgetItemsOrChangeBudgetActuals()
    {
        await using var db=await BudgetTestDatabase.CreateAsync();await Setup(db);
        var revisions=await db.Context().BudgetRevisions.CountAsync();var baseline=await db.Service().GetMonthAsync(new(2026,10,1));
        var component=new SavingsComponent{Name="Emergency",Mode="Monthly"};
        var fund=await Service(db).SaveAsync(new(){AccountId=1,Name="Emergency fund",Plan=new(){ScheduleContributions=false,StartDate=new(2026,10,16),Components=[component]}});
        fund=await Service(db).ActivityAsync(fund.SourceKey,new(){OperationId=Guid.NewGuid(),ExpectedEditVersion=fund.EditVersion,Date=new(2026,10,16),Kind="Contribution",Amounts=new(){{component.Key,100}}});
        var period=await db.Service().GetIncomePeriodAsync(new(2026,10,16));Assert.DoesNotContain(period.Contributions,e=>e.SourceKey==fund.SourceKey);
        var report=await new BudgetActualsService(db.Context(),new User(),db.Service(),db.Clock).GetPeriodAsync(new(2026,10,16),"all");
        Assert.DoesNotContain(report.Items,item=>item.AllocationKey==fund.SourceKey);Assert.Equal(0,report.AssignedActual);
        Assert.Equal(revisions,await db.Context().BudgetRevisions.CountAsync());Assert.Equal(baseline.Allocated,(await db.Service().GetMonthAsync(new(2026,10,1))).Allocated);
        Assert.Empty(await db.Context().BillPayments.ToListAsync());Assert.Equal(100,fund.Balance);
    }
    [Fact] public async Task AdoptsExistingBudgetItemWithoutChangingLedgerAndPreservesHistoricalPlan()
    {
        await using var db=await BudgetTestDatabase.CreateAsync();var revision=await Setup(db);var request=Request(revision);var service=Service(db);
        var review=await service.PreviewAsync(request);Assert.NotEmpty(review.Periods);var fund=await service.SaveAsync(request);
        Assert.Equal(revision.Allocations[0].SourceKey,fund.SourceKey);Assert.Equal(1000,fund.Balance);
        var account=Assert.Single(await service.GetAsync(1));Assert.Equal(20000,account.Balance);Assert.Equal(19000,account.Unallocated);
        Assert.Empty(await db.Context().Transactions.ToListAsync());
        var month=await db.Service().GetMonthAsync(new(2026,10,1));Assert.Single(month.Allocations,a=>a.SourceKey==fund.SourceKey);
        var old=await db.Service().GetIncomePeriodAsync(new(2026,10,2));Assert.Equal(225.81m,Assert.Single(old.Contributions).Amount);
        Assert.Empty((await Service(db,2).GetAsync()).SelectMany(a=>a.Funds));
        request.AccountId=2;await Assert.ThrowsAsync<ArgumentException>(()=>service.PreviewAsync(request));
    }
    [Fact] public async Task DraftGoalsStayOutOfPublishedBudgetAndSaveAtomicallyWithDraftVersion()
    {
        await using var db=await BudgetTestDatabase.CreateAsync();var draft=await Setup(db,false);var request=Request(draft);request.DraftId=draft.Id;request.DraftEditVersion=draft.EditVersion;
        var service=Service(db);var preview=await service.PreviewAsync(request);Assert.NotEmpty(preview.Periods);await service.SaveAsync(request);
        Assert.Null((await db.Service().GetMonthAsync(new(2026,10,1))).RevisionId);
        var saved=await db.Service().GetDraftAsync();Assert.NotNull(saved!.Allocations.Single(a=>a.SourceKey==request.SourceKey).SavingsPlan);
        await Assert.ThrowsAsync<BudgetConflictException>(()=>Service(db).SaveAsync(request));
        Assert.Single((await service.GetAsync(1)).Single().Funds);
    }
    [Fact] public async Task ContributionThenFundedExpenseCountsPurposeOnceAndKeepsReportsVisible()
    {
        await using var db=await BudgetTestDatabase.CreateAsync();var revision=await Setup(db);var request=Request(revision,1000);var fund=await Service(db).SaveAsync(request);var key=fund.Components[0].Definition.Key;
        var service=Service(db);var op=Guid.NewGuid();var activity=new SavingsActivityRequest{OperationId=op,ExpectedEditVersion=fund.EditVersion,Date=new(2026,10,16),Kind="Contribution",Amounts=new(){{key,100}}};
        fund=await service.ActivityAsync(fund.SourceKey,activity);var again=await Service(db).ActivityAsync(fund.SourceKey,activity);Assert.Equal(1100,again.Balance);
        var c=db.Context();c.Transactions.Add(new(){Id=1,HouseholdId=1,AccountId=3,CategoryId=1,TransactionDate=new(2026,10,16),Amount=350,Description="Repair"});await c.SaveChangesAsync();
        fund=await Service(db).ActivityAsync(fund.SourceKey,new(){OperationId=Guid.NewGuid(),ExpectedEditVersion=fund.EditVersion,Date=new(2026,10,16),Kind="Withdrawal",TransactionId=1,Amounts=new(){{key,-350}}});Assert.Equal(750,fund.Balance);
        var report=await new BudgetActualsService(db.Context(),new User(),db.Service(),db.Clock).GetPeriodAsync(new(2026,10,16),"all");
        Assert.Equal(350,Assert.Single(report.Transactions).Amount);Assert.Equal(350,Assert.Single(report.Transactions).FundedFromSavings);
        Assert.Equal(0,report.Points[0].ActualSpending);Assert.Equal(100,report.Points[0].ActualContributions);
        Assert.Equal(0,Assert.Single(report.Transactions).IsFundActivity?1:0);
    }
    [Fact] public async Task CannotOverallocateAndReallocationCreatesNoBankEntries()
    {
        await using var db=await BudgetTestDatabase.CreateAsync();var revision=await Setup(db);var request=Request(revision,20001);
        await Assert.ThrowsAsync<ArgumentException>(()=>Service(db).SaveAsync(request));Assert.Empty(await db.Context().Set<SavingsFund>().ToListAsync());
        request.OpeningAllocations[request.Plan.Components[0].Key]=1000;var second=new SavingsComponent{Name="Repairs",Mode="Monthly",Amount=100};request.Plan.Components.Add(second);var fund=await Service(db).SaveAsync(request);
        fund=await Service(db).ActivityAsync(fund.SourceKey,new(){OperationId=Guid.NewGuid(),ExpectedEditVersion=fund.EditVersion,Date=new(2026,10,16),Kind="Reallocate",Amounts=new(){{fund.Components[0].Definition.Key,-250},{second.Key,250}}});
        Assert.Equal(1000,fund.Balance);Assert.Equal(250,fund.Components[1].Balance);Assert.Empty(await db.Context().Transactions.ToListAsync());
    }
    [Fact] public async Task CalendarLinkFundsSavingsOnceAndUnlinkRemovesItsAllocation()
    {
        await using var db=await BudgetTestDatabase.CreateAsync();var revision=await Setup(db);var r=Request(revision,0);r.Plan.Components[0].Mode="Monthly";r.Plan.Components[0].Amount=100;var fund=await Service(db).SaveAsync(r);
        var c=db.Context();c.Transactions.Add(new(){Id=1,HouseholdId=1,AccountId=1,CategoryId=2,TransferDirection=TransferDirection.In,TransferCounterparty=TransferCounterparty.Unknown,TransactionDate=new(2026,10,16),Amount=100,Description="Saved"});await c.SaveChangesAsync();
        var occurrence=Assert.Single((await db.Service().GetIncomePeriodAsync(new(2026,10,16))).Contributions);
        var payment=new BillPaymentService(db.Context(),new User(),db.Service(),db.Clock);await payment.SaveAsync(fund.SourceKey,occurrence.Date,new(){IsPaid=true,TransactionIds=[1]});
        Assert.Equal(100,Assert.Single((await Service(db).GetAsync(1)).Single().Funds).Balance);
        var report=await new BudgetActualsService(db.Context(),new User(),db.Service(),db.Clock).GetPeriodAsync(new(2026,10,16),"all");Assert.Equal(100,report.Points[0].ActualContributions);
        await new BillPaymentService(db.Context(),new User(),db.Service(),db.Clock).SaveAsync(fund.SourceKey,occurrence.Date,new(){IsPaid=false,TransactionIds=[]});
        Assert.Equal(0,Assert.Single((await Service(db).GetAsync(1)).Single().Funds).Balance);
    }
    [Fact] public async Task EditingGoalReplacesItsContributionAndPreservesFundBalance()
    {
        await using var db=await BudgetTestDatabase.CreateAsync();var revision=await Setup(db);var r=Request(revision,1000);r.Plan.Components[0].Mode="Monthly";r.Plan.Components[0].Amount=100;var fund=await Service(db).SaveAsync(r);
        var management=await db.Service().GetManagementAsync();r.ExpectedRevisionId=management.Revision.Id;r.ExpectedEditVersion=fund.EditVersion;r.OpeningAllocations=[];r.Plan.Components[0].Amount=200;
        var preview=await Service(db).PreviewAsync(r);var p=preview.Periods[0];Assert.Equal(90.33m,p.Contribution); // 14/31 of $200
        fund=await Service(db).SaveAsync(r);Assert.Equal(1000,fund.Balance);
        var period=await db.Service().GetIncomePeriodAsync(new(2026,10,16));Assert.Equal(90.33m,Assert.Single(period.Contributions).Amount);
        await Assert.ThrowsAsync<BudgetConflictException>(()=>Service(db).ActivityAsync(fund.SourceKey,new(){OperationId=Guid.NewGuid(),ExpectedEditVersion=0,Date=new(2026,10,16),Amounts=new(){{fund.Components[0].Definition.Key,1}}}));
    }
    [Fact] public async Task MovesBetweenFundsAndReleasesCashWithoutAffectingTheLedger()
    {
        await using var db=await BudgetTestDatabase.CreateAsync();var revision=await Setup(db);var origin=await Service(db).SaveAsync(Request(revision));
        var latest=(await db.Service().GetManagementAsync()).Revision;var r=Request(latest,0);r.SourceKey=null;r.Name="Vacation";var destination=await Service(db).SaveAsync(r);
        var move=new MoveSavingsAllocation{OperationId=Guid.NewGuid(),ExpectedEditVersion=origin.EditVersion,ComponentKey=origin.Components[0].Definition.Key,DestinationFundKey=destination.SourceKey,DestinationComponentKey=destination.Components[0].Definition.Key,DestinationEditVersion=destination.EditVersion,Amount=250};
        origin=await Service(db).MoveAsync(origin.SourceKey,move);Assert.Equal(750,origin.Balance);
        var accounts=await Service(db).GetAsync(1);Assert.Equal(1000,accounts.Single().Allocated);Assert.Equal(250,accounts.Single().Funds.Single(f=>f.SourceKey==destination.SourceKey).Balance);
        Assert.Equal(750,(await Service(db).MoveAsync(origin.SourceKey,move)).Balance);
        origin=await Service(db).MoveAsync(origin.SourceKey,new(){OperationId=Guid.NewGuid(),ExpectedEditVersion=origin.EditVersion,ComponentKey=origin.Components[0].Definition.Key,Amount=100});Assert.Equal(650,origin.Balance);
        Assert.Equal(19100,(await Service(db).GetAsync(1)).Single().Unallocated);Assert.Empty(await db.Context().Transactions.ToListAsync());
    }
    [Fact] public async Task CorrectingAnAllocationRetainsHistoryAndRestoresActualSpending()
    {
        await using var db=await BudgetTestDatabase.CreateAsync();var revision=await Setup(db);var fund=await Service(db).SaveAsync(Request(revision));var component=fund.Components[0].Definition.Key;
        var c=db.Context();c.Transactions.Add(new(){Id=1,HouseholdId=1,AccountId=3,CategoryId=1,Amount=50,Description="Repair",TransactionDate=new(2026,10,16)});await c.SaveChangesAsync();
        var operation=Guid.NewGuid();fund=await Service(db).ActivityAsync(fund.SourceKey,new(){OperationId=operation,ExpectedEditVersion=fund.EditVersion,Date=new(2026,10,16),Kind="Withdrawal",TransactionId=1,Amounts=new(){{component,-50}}});
        fund=await Service(db).CorrectActivityAsync(fund.SourceKey,operation,fund.EditVersion);Assert.Equal(1000,fund.Balance);Assert.Contains(fund.Activities,a=>a.Kind=="Voided:WithdrawalLinked");Assert.Contains(fund.Activities,a=>a.Kind=="Correction");
        fund=await Service(db).ActivityAsync(fund.SourceKey,new(){OperationId=Guid.NewGuid(),ExpectedEditVersion=fund.EditVersion,Date=new(2026,10,16),Kind="Withdrawal",TransactionId=1,Amounts=new(){{component,-25}}});
        Assert.Equal(975,fund.Balance);
        var report=await new BudgetActualsService(db.Context(),new User(),db.Service(),db.Clock).GetPeriodAsync(new(2026,10,16),"all");Assert.Equal(25,report.Points[0].ActualSpending);
    }
    [Fact] public async Task FullyFundedTargetStopsFutureContributionsAndDeadlineOccursBeforePeriodEnd()
    {
        await using var db=await BudgetTestDatabase.CreateAsync();var revision=await Setup(db);var r=Request(revision,0);r.Plan.Components[0].Mode="Target";r.Plan.Components[0].TargetDate=new(2026,10,23);r.Plan.Components[0].Amount=100;var fund=await Service(db).SaveAsync(r);
        var eventBefore=Assert.Single((await db.Service().GetIncomePeriodAsync(new(2026,10,16))).Contributions);Assert.Equal(new DateOnly(2026,10,23),eventBefore.Date);Assert.Equal(100,eventBefore.Amount);
        fund=await Service(db).ActivityAsync(fund.SourceKey,new(){OperationId=Guid.NewGuid(),ExpectedEditVersion=fund.EditVersion,Date=new(2026,10,16),Amounts=new(){{fund.Components[0].Definition.Key,100}}});
        Assert.Empty((await db.Service().GetIncomePeriodAsync(new(2026,10,30))).Contributions);
        var report=await new BudgetActualsService(db.Context(),new User(),db.Service(),db.Clock).GetPeriodAsync(new(2026,10,16),"all");Assert.Equal(100,report.Points[^1].ForecastContributions);
    }
    [Fact] public async Task ShortfallRequiresAcknowledgmentAndDoesNotCreateFundOnRejectedSave()
    {
        await using var db=await BudgetTestDatabase.CreateAsync();var revision=await Setup(db);var r=Request(revision,0);r.Plan.Components[0].Mode="Target";r.Plan.Components[0].Amount=100000;r.Plan.Components[0].TargetDate=new(2026,10,29);
        r.AcceptShortfall=false;var review=await Service(db).PreviewAsync(r);Assert.True(review.LargestShortfall>0);
        await Assert.ThrowsAsync<ArgumentException>(()=>Service(db).SaveAsync(r));Assert.Empty(await db.Context().Set<SavingsFund>().ToListAsync());
        r.AcceptShortfall=true;Assert.NotNull(await Service(db).SaveAsync(r));
    }

    [Fact] public async Task AssigningExistingCashDoesNotBecomeNewBudgetSpendingOrIncome()
    {
        await using var db=await BudgetTestDatabase.CreateAsync();var revision=await Setup(db);var r=Request(revision,0);r.Plan.Components[0].Mode="Monthly";r.Plan.Components[0].Amount=0;var fund=await Service(db).SaveAsync(r);
        fund=await Service(db).ActivityAsync(fund.SourceKey,new(){OperationId=Guid.NewGuid(),ExpectedEditVersion=fund.EditVersion,Date=new(2026,10,16),Kind="Allocate",Amounts=new(){{fund.Components[0].Definition.Key,500}}});Assert.Equal(500,fund.Balance);
        var report=await new BudgetActualsService(db.Context(),new User(),db.Service(),db.Clock).GetPeriodAsync(new(2026,10,16));Assert.All(report.Points,p=>Assert.Equal(0,p.ActualContributions??0));Assert.Empty(await db.Context().Transactions.ToListAsync());
    }
    [Fact] public async Task ContributionReviewExtendsThroughLongDatedTargets()
    {
        await using var db=await BudgetTestDatabase.CreateAsync();var revision=await Setup(db);var r=Request(revision,0);r.Plan.Components[0].Mode="Target";r.Plan.Components[0].TargetDate=new(2029,10,16);r.Plan.Components[0].Amount=3000;
        var review=await Service(db).PreviewAsync(r);Assert.True(review.Periods[^1].EndDate>=r.Plan.Components[0].TargetDate);Assert.Equal(3000,review.Periods.Sum(p=>p.Contribution));
    }

    [Fact] public async Task MissedTargetIsReservedInCurrentPeriodInsteadOfDisappearing()
    {
        await using var db=await BudgetTestDatabase.CreateAsync();var revision=await Setup(db);var r=Request(revision,0);r.Plan.Components[0].Mode="Target";r.Plan.Components[0].TargetDate=new(2026,10,23);r.Plan.Components[0].Amount=100;await Service(db).SaveAsync(r);
        db.Clock.Now=new(2026,10,30,16,0,0,TimeSpan.Zero);
        var period=await db.Service().GetIncomePeriodAsync();var contribution=Assert.Single(period.Contributions);Assert.Equal(100,contribution.Amount);Assert.Equal(new DateOnly(2026,11,12),contribution.Date);
    }

    [Fact] public async Task SkippingSavingsRemovesPeriodAndMonthlyPlansAndForecastThenRestores()
    {
        await using var db=await BudgetTestDatabase.CreateAsync();var revision=await Setup(db);var r=Request(revision,0);r.Plan.Components[0].Mode="Monthly";r.Plan.Components[0].Amount=100;var fund=await Service(db).SaveAsync(r);
        var before=await db.Service().GetIncomePeriodAsync(new(2026,10,16));var occurrence=Assert.Single(before.Contributions);
        var beforeMonth=(await db.Service().GetMonthAsync(new(2026,10,1))).Allocations.Single(a=>a.SourceKey==fund.SourceKey).PlannedAmount;
        var payment=new BillPaymentService(db.Context(),new User(),db.Service(),db.Clock);
        var skipped=await payment.SaveAsync(fund.SourceKey,occurrence.Date,new(){IsSkipped=true,IsPaid=false});Assert.True(skipped.IsSkipped);
        var period=await db.Service().GetIncomePeriodAsync(new(2026,10,16));Assert.Equal(0,period.ContributionsTotal);Assert.True(Assert.Single(period.Contributions).Payment!.IsSkipped);Assert.Equal(before.Plan!.Allocated-occurrence.Amount,period.Plan!.Allocated);
        var report=await new BudgetActualsService(db.Context(),new User(),db.Service(),db.Clock).GetPeriodAsync(new(2026,10,16));Assert.Equal(0,report.Points[^1].ForecastContributions);Assert.Equal(0,report.Points[^1].PlannedContributions);
        Assert.Equal(beforeMonth-occurrence.Amount,(await db.Service().GetMonthAsync(new(2026,10,1))).Allocations.Single(a=>a.SourceKey==fund.SourceKey).PlannedAmount);
        Assert.True((await db.Service().GetIncomePeriodAsync(new(2026,10,30))).ContributionsTotal>0);Assert.Equal(0,(await Service(db).GetAsync(1)).Single().Funds.Single().Balance);
        await new BillPaymentService(db.Context(),new User(),db.Service(),db.Clock).SaveAsync(fund.SourceKey,occurrence.Date,new(){IsSkipped=false,IsPaid=false});
        Assert.Equal(occurrence.Amount,(await db.Service().GetIncomePeriodAsync(new(2026,10,16))).ContributionsTotal);
    }
    [Fact] public async Task SkippingCrossMonthPeriodZeroesBothMonthPortions()
    {
        await using var db=await BudgetTestDatabase.CreateAsync();var revision=await Setup(db);var key=revision.Allocations[0].SourceKey!.Value;
        var period=await db.Service().GetIncomePeriodAsync(new(2026,10,30));var occurrence=Assert.Single(period.Contributions);
        await new BillPaymentService(db.Context(),new User(),db.Service(),db.Clock).SaveAsync(key,occurrence.Date,new(){IsSkipped=true});
        Assert.Equal(0,(await db.Service().GetIncomePeriodAsync(new(2026,10,30))).ContributionsTotal);
        Assert.Equal(500-32.26m,(await db.Service().GetMonthAsync(new(2026,10,1))).Allocations.Single(a=>a.SourceKey==key).PlannedAmount);
        Assert.Equal(500-200m,(await db.Service().GetMonthAsync(new(2026,11,1))).Allocations.Single(a=>a.SourceKey==key).PlannedAmount);
    }

    [Fact] public async Task SkippingRemainingContributionRetainsActualMoneyAlreadySetAside()
    {
        await using var db=await BudgetTestDatabase.CreateAsync();var revision=await Setup(db);var r=Request(revision,0);r.Plan.Components[0].Mode="Monthly";r.Plan.Components[0].Amount=100;var fund=await Service(db).SaveAsync(r);
        fund=await Service(db).ActivityAsync(fund.SourceKey,new(){OperationId=Guid.NewGuid(),ExpectedEditVersion=fund.EditVersion,Date=new(2026,10,16),Amounts=new(){{fund.Components[0].Definition.Key,10}}});
        var occurrence=Assert.Single((await db.Service().GetIncomePeriodAsync(new(2026,10,16))).Contributions);
        await new BillPaymentService(db.Context(),new User(),db.Service(),db.Clock).SaveAsync(fund.SourceKey,occurrence.Date,new(){IsSkipped=true});
        var report=await new BudgetActualsService(db.Context(),new User(),db.Service(),db.Clock).GetPeriodAsync(new(2026,10,16));Assert.Equal(10,report.Points[0].ActualContributions);Assert.Equal(10,report.Points[^1].ForecastContributions);Assert.Equal(0,report.Points[^1].PlannedContributions);
        Assert.Equal(10,(await Service(db).GetAsync(1)).Single().Funds.Single().Balance);
    }

}
