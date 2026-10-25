using Coiny.API.DTOs.MerchantRules;
using Coiny.API.Enums;
using Coiny.API.Interfaces;
using Coiny.API.Models;
using Coiny.API.Services;
using Microsoft.EntityFrameworkCore;
namespace Coiny.API.Tests;
public class MerchantRulesTests
{
    private sealed class User(int household=1):ICurrentUserService {
        public bool IsAuthenticated()=>true;public string? GetUserId()=>"test";public Task<int> GetCurrentHouseholdIdAsync()=>Task.FromResult(household);
    }
    private static MerchantRulesService Service(BudgetTestDatabase db,int household=1)=>new(db.Context(),new User(household));
    private static MerchantRuleInput Input(int category=1,int version=0)=>new(){MatchType="MerchantExact",Pattern="Netflix.com",CategoryId=category,IsActive=true,ExpectedEditVersion=version};
    private static async Task Seed(BudgetTestDatabase db){var c=db.Context();c.Categories.AddRange(new Category {Id=1,HouseholdId=1,Name="Subscriptions",IsActive=true,CategoryType=CategoryType.Expense},new Category {Id=2,HouseholdId=1,Name="Archived",CategoryType=CategoryType.Expense,IsActive=false},new Category {Id=3,HouseholdId=1,Name="Transfers",CategoryType=CategoryType.Transfer},new Category {Id=4,HouseholdId=2,Name="Foreign",IsActive=true,CategoryType=CategoryType.Expense});await c.SaveChangesAsync();}
    [Fact]public async Task Rules_are_household_scoped_and_updates_deletes_reject_stale_versions(){
        await using var db=await BudgetTestDatabase.CreateAsync();await Seed(db);var r=await Service(db).SaveAsync(null,Input());Assert.Equal(1,r.EditVersion);Assert.Equal("Subscriptions",r.CategoryName);
        Assert.Empty(await Service(db,2).ListAsync());await Assert.ThrowsAsync<ArgumentException>(()=>Service(db,2).SaveAsync(r.Id,Input(4,1)));
        await Assert.ThrowsAsync<BudgetConflictException>(()=>Service(db).SaveAsync(r.Id,Input()));
        await Assert.ThrowsAsync<BudgetConflictException>(()=>Service(db).DeleteAsync(r.Id,0));
        var edited=await Service(db).SaveAsync(r.Id,new(){MatchType="DescriptionContains",Pattern="NETFLIX PAYMENT",CategoryId=1,IsActive=false,ExpectedEditVersion=1});Assert.False(edited.IsActive);Assert.Equal(2,edited.EditVersion);
        await Service(db).DeleteAsync(r.Id,2);Assert.Empty(await Service(db).ListAsync());
    }
    [Fact]public async Task Targets_and_phrases_are_validated_and_normalized_duplicates_are_rejected(){
        await using var db=await BudgetTestDatabase.CreateAsync();await Seed(db);
        foreach(var cat in new[]{2,3,4})await Assert.ThrowsAsync<ArgumentException>(()=>Service(db).SaveAsync(null,Input(cat)));
        await Service(db).SaveAsync(null,Input());var duplicate=Input();duplicate.Pattern=" NETFLIX.COM ";await Assert.ThrowsAsync<BudgetConflictException>(()=>Service(db).SaveAsync(null,duplicate));
        var shortRule=Input();shortRule.Pattern="!!!";await Assert.ThrowsAsync<ArgumentException>(()=>Service(db).SaveAsync(null,shortRule));
        var invalid=Input();invalid.MatchType="Regex";await Assert.ThrowsAsync<ArgumentException>(()=>Service(db).SaveAsync(null,invalid));
    }
    [Fact]public async Task Preview_only_reads_matching_household_transactions_and_archiving_a_category_disables_suggestions(){
        await using var db=await BudgetTestDatabase.CreateAsync();await Seed(db);var c=db.Context();c.Accounts.Add(new(){Id=1,HouseholdId=1,Name="Checking"});
        c.Transactions.Add(new(){Id=1,HouseholdId=1,AccountId=1,CategoryId=1,Merchant="NETFLIX.COM",Description="Netflix monthly charge",Amount=20,TransactionDate=new(2026,10,20)});await c.SaveChangesAsync();
        var r=await Service(db).SaveAsync(null,Input());var preview=await Service(db).PreviewAsync(Input());Assert.Equal(1,preview.TransactionsChecked);Assert.Equal(1,Assert.Single(preview.Examples).Id);
        var description=Input();description.MatchType="DescriptionContains";description.Pattern="MONTHLY CHARGE";Assert.Single((await Service(db).PreviewAsync(description)).Examples);
        Assert.Empty((await Service(db,2).PreviewAsync(Input(4))).Examples);Assert.Equal(0,(await db.Context().Transactions.SingleAsync()).EditVersion);
        var c2=db.Context();(await c2.Categories.SingleAsync(c=>c.Id==1)).IsActive=false;await c2.SaveChangesAsync();Assert.False(Assert.Single(await Service(db).ListAsync()).CategoryActive);
        var disabled=Input(1,r.EditVersion);disabled.IsActive=false;Assert.False((await Service(db).SaveAsync(r.Id,disabled)).IsActive);
    }
}
