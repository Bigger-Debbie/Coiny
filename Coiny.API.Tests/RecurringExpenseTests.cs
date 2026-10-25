using Coiny.API.DTOs.Recurring;
using Coiny.API.Enums;
using Coiny.API.Interfaces;
using Coiny.API.Models;
using Coiny.API.Services;
using Microsoft.EntityFrameworkCore;
namespace Coiny.API.Tests;
public class RecurringExpenseTests
{
    private sealed class User(int household=1):ICurrentUserService {
        public bool IsAuthenticated()=>true; public string? GetUserId()=>"test";
        public Task<int> GetCurrentHouseholdIdAsync()=>Task.FromResult(household);
    }
    private static RecurringExpenseService Service(BudgetTestDatabase db,int household=1)=>new(db.Context(),new User(household),db.Service(household),db.Clock);
    private static RecurringInput Row(int id,string date,decimal amount=20,string merchant="Netflix")=>new(id,merchant,DateOnly.Parse(date),amount,"Checking","Recorded charge");
    [Theory]
    [InlineData("2026-10-02","2026-10-09","2026-10-16","Weekly")]
    [InlineData("2026-09-18","2026-10-02","2026-10-16","EveryTwoWeeks")]
    [InlineData("2026-08-16","2026-09-16","2026-10-16","Monthly")]
    [InlineData("2025-10-16","2026-04-16","2026-10-16","EverySixMonths")]
    [InlineData("2024-10-16","2025-10-16","2026-10-16","Yearly")]
    public void Detects_supported_cadences_and_keeps_stable_identity(string first,string second,string third,string cadence)
    {
        var rows=new[]{Row(1,first,20," NETFLIX.COM "),Row(2,second,21,"netflix.com"),Row(3,third,22,"Netflix.Com")};
        var p=Assert.Single(RecurringExpenseDetector.Detect(rows,new(2026,10,16)));
        Assert.Equal(cadence,p.Frequency); Assert.Equal(21,p.TypicalAmount); Assert.Equal(20,p.MinimumAmount); Assert.Equal(22,p.MaximumAmount);
        Assert.Equal(3,p.TransactionCount); Assert.Equal(3,p.Transactions.Count); Assert.True(p.NextExpected>p.LastSeen);
        Assert.Equal(p.Key,Assert.Single(RecurringExpenseDetector.Detect(rows.Reverse(),new(2026,10,16))).Key);
    }
    [Fact] public void Rejects_insufficient_irregular_ambiguous_stale_and_highly_variable_patterns()
    {
        var today=new DateOnly(2026,10,16);
        var regular=new[]{Row(1,"2026-08-16"),Row(2,"2026-09-16"),Row(3,"2026-10-16")};
        Assert.Empty(RecurringExpenseDetector.Detect(regular.Take(2),today));
        Assert.Empty(RecurringExpenseDetector.Detect([regular[0],regular[1] with { Date=new(2026,9,2) },regular[2]],today));
        Assert.Empty(RecurringExpenseDetector.Detect([..regular,Row(4,"2026-10-16")],today));
        Assert.Empty(RecurringExpenseDetector.Detect(regular,new(2027,3,1)));
        Assert.Empty(RecurringExpenseDetector.Detect([regular[0],regular[1],regular[2] with { Amount=200 }],today));
        Assert.Empty(RecurringExpenseDetector.Detect(regular.Select((r,i)=>r with { Merchant="Store "+i }),today));
        Assert.Empty(RecurringExpenseDetector.Detect(regular.Select(r=>r with { Date=r.Date.AddYears(1) }),today));
    }
    private static async Task Ledger(BudgetTestDatabase db)
    {
        var c=db.Context();
        c.Accounts.AddRange(new Account { Id=1,HouseholdId=1,Name="Checking" },new Account { Id=2,HouseholdId=2,Name="Foreign" });
        c.Categories.AddRange(new Category { Id=1,HouseholdId=1,Name="Subscriptions",CategoryType=CategoryType.Expense },new Category { Id=2,HouseholdId=1,Name="Transfer",CategoryType=CategoryType.Transfer },new Category { Id=3,HouseholdId=2,Name="Foreign",CategoryType=CategoryType.Expense },new Category { Id=4,HouseholdId=1,Name="Income",CategoryType=CategoryType.Income });
        for(var i=0;i<3;i++) foreach(var category in new[]{1,2,3,4}) c.Transactions.Add(new() { HouseholdId=category==3?2:1,AccountId=category==3?2:1,CategoryId=category,Merchant=category==1?"Netflix":"Other",Description="Charge",Amount=20,TransactionDate=new(2026,8+i,16) });
        c.Transactions.Add(new() { HouseholdId=1,AccountId=1,CategoryId=1,Merchant="Netflix",Amount=-20,TransactionDate=new(2026,10,16) });
        c.Transactions.Add(new() { HouseholdId=1,AccountId=1,CategoryId=1,Merchant="Netflix",Amount=20,TransactionDate=new(2027,10,16) });
        await c.SaveChangesAsync();
    }
    [Fact] public async Task Scan_is_scoped_read_only_and_reviews_persist_without_touching_ledger()
    {
        await using var db=await BudgetTestDatabase.CreateAsync(); await Ledger(db);
        var before=await db.Context().Transactions.AsNoTracking().OrderBy(t=>t.Id).Select(t=>new {t.Id,t.Amount,t.EditVersion,t.PairedTransactionId}).ToListAsync();
        var service=Service(db); var scan=await service.ScanAsync(); var p=Assert.Single(scan.Suggestions);
        Assert.Equal(3,scan.TransactionsScanned); Assert.Equal("Netflix",p.Merchant); Assert.Equal("NeedsReview",p.Status);
        Assert.Equal(0,await db.Context().RecurringExpenseReviews.CountAsync());
        await service.ReviewAsync(new() { Key=p.Key,Action="Dismiss" });
        Assert.Equal("Dismissed",Assert.Single((await Service(db).ScanAsync()).Suggestions).Status);
        Assert.Equal("NeedsReview",Assert.Single((await Service(db,2).ScanAsync()).Suggestions).Status);
        await Service(db).ReviewAsync(new() { Key=p.Key,Action="Reset" });
        Assert.Equal("NeedsReview",Assert.Single((await Service(db).ScanAsync()).Suggestions).Status);
        Assert.Equal(before,await db.Context().Transactions.AsNoTracking().OrderBy(t=>t.Id).Select(t=>new {t.Id,t.Amount,t.EditVersion,t.PairedTransactionId}).ToListAsync());
        await Assert.ThrowsAsync<ArgumentException>(()=>service.ReviewAsync(new() { Key="not-a-current-pattern",Action="Dismiss" }));
        await Assert.ThrowsAsync<ArgumentException>(()=>service.ReviewAsync(new() { Key=p.Key,Action="Match",AllocationKey=Guid.NewGuid() }));
    }
    [Fact] public async Task Match_requires_current_budget_target_and_dismissal_reopens_after_material_amount_change()
    {
        await using var db=await BudgetTestDatabase.CreateAsync(); await Ledger(db);
        var budgets=db.Service(); var draft=await budgets.StartDraftAsync();
        var saved=await budgets.SaveDraftAsync(draft.Id,new() { ExpectedEditVersion=draft.EditVersion,EffectiveFromMonth=new(2026,10,1),SetupStep=BudgetSetupStep.Review,
            IncomeSources=[new() { Name="Salary",Amount=1000,Schedule=new() { Frequency=BudgetFrequency.Monthly,StartDate=new(2026,10,1),DayOfMonth=1 } }],
            Allocations=[new() { Name="Netflix",Amount=20,AllocationType=BudgetAllocationType.Subscriptions,Schedule=new() { Frequency=BudgetFrequency.Monthly,StartDate=new(2026,10,1),DayOfMonth=16 } }] });
        await budgets.PublishDraftAsync(saved.Id,saved.EditVersion);
        var key=saved.Allocations.Single().SourceKey!.Value;
        var service=Service(db);var p=Assert.Single((await service.ScanAsync()).Suggestions);
        Assert.Equal("NeedsReview",p.Status); Assert.Equal(key,p.SuggestedAllocationKey);
        await service.ReviewAsync(new() { Key=p.Key,Action="Match",AllocationKey=key });
        Assert.Equal("Covered",Assert.Single((await Service(db).ScanAsync()).Suggestions).Status);
        Assert.Equal(0,await db.Context().BillPayments.CountAsync());
        await Service(db).ReviewAsync(new() { Key=p.Key,Action="Dismiss" });
        var c=db.Context();foreach(var t in await c.Transactions.Where(t=>t.HouseholdId==1 && t.CategoryId==1 && t.Amount>0).ToListAsync()) t.Amount=30;
        await c.SaveChangesAsync();
        Assert.Equal("NeedsReview",Assert.Single((await Service(db).ScanAsync()).Suggestions).Status);
    }
}
