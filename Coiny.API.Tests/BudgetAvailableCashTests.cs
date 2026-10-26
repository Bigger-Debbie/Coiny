using Coiny.API.DTOs.Budgets;
using Coiny.API.Enums;
using Coiny.API.Interfaces;
using Coiny.API.Models;
using Coiny.API.Services;
using Microsoft.EntityFrameworkCore;
namespace Coiny.API.Tests;
public class BudgetAvailableCashTests
{
    private sealed class User : ICurrentUserService
    { public string? GetUserId()=>"test"; public bool IsAuthenticated()=>true; public Task<int> GetCurrentHouseholdIdAsync()=>Task.FromResult(1); }
    private static BudgetActualsService Service(BudgetTestDatabase db) => new(db.Context(),new User(),db.Service(),db.Clock);
    private static async Task<Guid> Seed(BudgetTestDatabase db)
    {
        db.Clock.Now=new(2026,10,1,16,0,0,TimeSpan.Zero);
        var budgets=db.Service(); var draft=await budgets.StartDraftAsync();
        var saved=await budgets.SaveDraftAsync(draft.Id,new() {
            ExpectedEditVersion=draft.EditVersion,EffectiveFromMonth=new(2026,10,1),SetupStep=BudgetSetupStep.Review,
            IncomeSources=[new() { Name="Pay",Amount=1000,Schedule=new() { Frequency=BudgetFrequency.EveryTwoWeeks,StartDate=new(2026,10,2) } }],
            Allocations=[new() { Name="Utility",Amount=100,AllocationType=BudgetAllocationType.Bills,Schedule=new() { Frequency=BudgetFrequency.Monthly,StartDate=new(2026,10,1),DayOfMonth=10 } },
                new() { Name="Car fund",Amount=50,AllocationType=BudgetAllocationType.Savings,Schedule=new() { Frequency=BudgetFrequency.Monthly,StartDate=new(2026,10,1),DayOfMonth=14 } }]
        }); await budgets.PublishDraftAsync(saved.Id,saved.EditVersion);
        var c=db.Context();
        c.Accounts.AddRange(new Account { Id=1,HouseholdId=1,Name="Checking",OpeningBalance=1000,OpeningBalanceDate=new(2026,10,1) },
            new Account { Id=2,HouseholdId=1,Name="Emergency",AccountType=AccountType.Savings,OpeningBalance=5000,OpeningBalanceDate=new(2026,10,1) },
            new Account { Id=3,HouseholdId=1,Name="Card",AccountType=AccountType.CreditCard,OpeningBalance=200,OpeningBalanceDate=new(2026,10,1) },
            new Account { Id=4,HouseholdId=2,Name="Foreign",OpeningBalance=7000,OpeningBalanceDate=new(2026,10,1) },
            new Account { Id=5,HouseholdId=1,Name="Mortgage",AccountType=AccountType.Loan,OpeningBalance=9000,OpeningBalanceDate=new(2026,10,1) });
        c.Categories.AddRange(new Category { Id=1,HouseholdId=1,Name="Pay",CategoryType=CategoryType.Income },new Category { Id=2,HouseholdId=1,Name="Bills",CategoryType=CategoryType.Expense },new Category { Id=3,HouseholdId=1,Name="Transfer",CategoryType=CategoryType.Transfer });
        c.Transactions.AddRange(new Transaction { Id=1,HouseholdId=1,AccountId=1,CategoryId=1,Amount=1000,Description="Pay",TransactionDate=new(2026,10,2) },
            new Transaction { Id=2,HouseholdId=1,AccountId=3,CategoryId=2,Amount=100,Description="Utility",TransactionDate=new(2026,10,7) },
            new Transaction { Id=3,HouseholdId=1,AccountId=1,CategoryId=3,TransferDirection=TransferDirection.Out,TransferCounterparty=TransferCounterparty.Unknown,Amount=80,Description="Card payment",TransactionDate=new(2026,10,8) },
            new Transaction { Id=4,HouseholdId=1,AccountId=3,CategoryId=3,TransferDirection=TransferDirection.In,TransferCounterparty=TransferCounterparty.Unknown,Amount=80,Description="Card payment",TransactionDate=new(2026,10,8) },
            new Transaction { Id=5,HouseholdId=1,AccountId=1,CategoryId=1,Amount=999,Description="Future pay",TransactionDate=new(2026,11,2) });
        await c.SaveChangesAsync(); db.Clock.Now=new(2026,10,8,16,0,0,TimeSpan.Zero);
        return saved.Allocations.Single(a=>a.Name=="Utility").SourceKey!.Value;
    }
    [Fact] public async Task Uses_current_cash_and_card_debt_without_repeating_posted_expenses_or_income()
    {
        await using var db=await BudgetTestDatabase.CreateAsync(); var key=await Seed(db);
        var result=await Service(db).GetAvailableCashAsync(new());
        Assert.Equal(new[] {1,3},result.AccountIds.Order().ToArray()); Assert.Equal(1920,result.CashBalance); Assert.Equal(220,result.CardBalance);
        Assert.Equal(0,result.ExpectedIncome); Assert.Equal(150,result.RemainingAllocations); Assert.Equal(1550,result.ExpectedRemaining);
        Assert.DoesNotContain(result.Accounts,a=>a.Id is 4 or 5); Assert.DoesNotContain(2,result.AccountIds);
        await new BillPaymentService(db.Context(),new User(),db.Service(),db.Clock).SaveAsync(key,new(2026,10,10),new() {IsPaid=true,TransactionId=2});
        result=await Service(db).GetAvailableCashAsync(new());
        Assert.Equal(50,result.RemainingAllocations); Assert.Equal(1650,result.ExpectedRemaining);
        var withSavings=await Service(db).GetAvailableCashAsync(new() {AccountIds=[1,2,3]}); Assert.Equal(6650,withSavings.ExpectedRemaining);
        var noCash=await Service(db).GetAvailableCashAsync(new() {AccountIds=[3]}); Assert.Null(noCash.ExpectedRemaining);
        Assert.Equal(5,await db.Context().Transactions.CountAsync()); Assert.Empty(await db.Context().AccountBalanceSnapshots.ToListAsync());
    }
    [Fact] public async Task Includes_all_intervening_periods_for_future_end_dates_and_rejects_foreign_or_unsupported_accounts()
    {
        await using var db=await BudgetTestDatabase.CreateAsync(); await Seed(db);
        var result=await Service(db).GetAvailableCashAsync(new() {AsOf=new(2026,10,16)});
        Assert.Equal(new DateOnly(2026,10,29),result.EndDate); Assert.Equal(1000,result.ExpectedIncome);
        Assert.Equal(150,result.RemainingAllocations); Assert.Equal(2550,result.ExpectedRemaining);
        foreach(var ids in new[] {new[]{4},new[]{5},new[]{1,1}})
            await Assert.ThrowsAsync<ArgumentException>(()=>Service(db).GetAvailableCashAsync(new() {AccountIds=ids}));
        await Assert.ThrowsAsync<ArgumentException>(()=>Service(db).GetAvailableCashAsync(new() {AsOf=new(2026,9,1)}));
    }
}
