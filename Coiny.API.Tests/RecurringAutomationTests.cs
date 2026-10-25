using Coiny.API.Enums;
using Coiny.API.Interfaces;
using Coiny.API.Models;
using Coiny.API.Services;
using Microsoft.EntityFrameworkCore;
namespace Coiny.API.Tests;
public class RecurringAutomationTests
{
    private sealed class User(string uid="one",int household=1):ICurrentUserService {
        public bool IsAuthenticated()=>true;public string? GetUserId()=>uid;public Task<int> GetCurrentHouseholdIdAsync()=>Task.FromResult(household);
    }
    private static RecurringAutomationService Auto(BudgetTestDatabase db)=>new(db.Context(),new BudgetScheduleService(),db.Clock);
    private static HouseholdAlertsService Alerts(BudgetTestDatabase db,string uid="one",int hh=1)=>new(db.Context(),new User(uid,hh));
    private static async Task Seed(BudgetTestDatabase db) {
        var c=db.Context();c.Users.AddRange(new ApplicationUser {Id="one",UserName="one"},new ApplicationUser {Id="two",UserName="two"});
        c.Accounts.Add(new Account {Id=1,HouseholdId=1,Name="Checking"});
        c.Categories.Add(new Category {Id=1,HouseholdId=1,Name="Subscription",CategoryType=CategoryType.Expense});
        foreach(var month in new[]{8,9,10})c.Transactions.Add(new() {HouseholdId=1,AccountId=1,CategoryId=1,Amount=20,Merchant="Netflix",TransactionDate=new(2026,month,16)});
        await c.SaveChangesAsync();
    }
    [Fact] public async Task Shared_settings_save_is_atomic_and_rejects_stale_household_versions() {
        await using var db=await BudgetTestDatabase.CreateAsync();await Seed(db);await Auto(db).SaveAsync(1,true,0);
        var c=db.Context();var automation=new RecurringAutomationService(c,new BudgetScheduleService(),db.Clock);
        await Assert.ThrowsAsync<BudgetConflictException>(()=>new UserPreferencesService(c,new User()).SaveWithRecurringAsync(false,false,0,automation));
        Assert.True((await new UserPreferencesService(db.Context(),new User()).GetAsync()).IncludeMortgageInNetWorth);Assert.True((await Auto(db).GetAsync(1)).Enabled);
        var c2=db.Context();await new UserPreferencesService(c2,new User()).SaveWithRecurringAsync(false,false,1,new(c2,new BudgetScheduleService(),db.Clock));
        Assert.False((await new UserPreferencesService(db.Context(),new User()).GetAsync()).IncludeMortgageInNetWorth);Assert.False((await Auto(db).GetAsync(1)).Enabled);
        var c3=db.Context();await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>new UserPreferencesService(c3,new User("missing")).SaveWithRecurringAsync(true,true,2,new(c3,new BudgetScheduleService(),db.Clock)));
        Assert.False((await Auto(db).GetAsync(1)).Enabled);Assert.Equal(2,(await Auto(db).GetAsync(1)).EditVersion);
    }
    [Fact] public async Task Disabled_is_default_and_scans_run_once_per_local_day_across_restarts_without_duplicate_alerts() {
        await using var db=await BudgetTestDatabase.CreateAsync();await Seed(db);
        Assert.False((await Auto(db).GetAsync(1)).Enabled);Assert.False(await Auto(db).ProcessAsync(1));
        await Auto(db).SaveAsync(1,true,0);Assert.True(await Auto(db).ProcessAsync(1));Assert.False(await Auto(db).ProcessAsync(1));
        Assert.Equal(new DateOnly(2026,10,16),(await Auto(db).GetAsync(1)).LastScanDate);
        Assert.Equal(1,await Alerts(db).CountAsync());var alert=Assert.Single((await Alerts(db).ListAsync()).Items);
        Assert.Equal(1,alert.Version);Assert.Equal("Netflix",alert.Merchant);
        await Alerts(db).ReadAsync(alert.Id,alert.Version);Assert.Equal(0,await Alerts(db).CountAsync());Assert.Equal(1,await Alerts(db,"two").CountAsync());
        Assert.Equal(0,await Alerts(db,"one",2).CountAsync());
        await Assert.ThrowsAsync<ArgumentException>(()=>Alerts(db,"one",2).ReadAsync(alert.Id,alert.Version));
        await Assert.ThrowsAsync<ArgumentException>(()=>Alerts(db).ReadAsync(alert.Id,alert.Version+1));
        db.Clock.Now=new(2026,10,17,2,0,0,TimeSpan.Zero);Assert.False(await Auto(db).ProcessAsync(1)); // Still Oct 16 New York.
        db.Clock.Now=new(2026,10,17,12,0,0,TimeSpan.Zero);Assert.True(await Auto(db).ProcessAsync(1));
        Assert.Equal(0,await Alerts(db).CountAsync());Assert.Equal(1,await db.Context().HouseholdAlerts.CountAsync());
        var setting=await Auto(db).GetAsync(1);await Auto(db).SaveAsync(1,false,setting.EditVersion);
        db.Clock.Now=new(2026,10,18,12,0,0,TimeSpan.Zero);Assert.False(await Auto(db).ProcessAsync(1));Assert.Equal(1,await Alerts(db,"two").CountAsync());
    }
    [Fact] public async Task Material_changes_reopen_alerts_and_dismissal_resolves_for_all_members_without_ledger_changes() {
        await using var db=await BudgetTestDatabase.CreateAsync();await Seed(db);await Auto(db).SaveAsync(1,true,0);await Auto(db).ProcessAsync(1);
        var first=Assert.Single((await Alerts(db).ListAsync()).Items);await Alerts(db).ReadAsync(first.Id,first.Version);
        var c=db.Context();foreach(var t in await c.Transactions.ToListAsync())t.Amount=30;await c.SaveChangesAsync();
        db.Clock.Now=new(2026,10,17,12,0,0,TimeSpan.Zero);await Auto(db).ProcessAsync(1);
        var changed=Assert.Single((await Alerts(db).ListAsync()).Items);Assert.Equal(first.Id,changed.Id);Assert.Equal(2,changed.Version);Assert.Equal(1,await Alerts(db).CountAsync());
        await Alerts(db).ReadAsync(changed.Id,1);Assert.Equal(1,await Alerts(db).CountAsync()); // Reading an old version cannot clear a newer alert.
        var service=new RecurringExpenseService(db.Context(),new User(),db.Service(),db.Clock);
        var suggestion=Assert.Single((await service.ScanAsync()).Suggestions);await service.ReviewAsync(new() {Key=suggestion.Key,Action="Dismiss"});
        Assert.Equal(0,await Alerts(db).CountAsync());Assert.Equal(0,await Alerts(db,"two").CountAsync());Assert.Empty((await Alerts(db).ListAsync()).Items);
        var before=await db.Context().Transactions.Select(t=>new {t.Id,t.Amount,t.EditVersion}).ToListAsync();
        db.Clock.Now=new(2026,10,18,12,0,0,TimeSpan.Zero);await Auto(db).ProcessAsync(1);
        Assert.Equal(before,await db.Context().Transactions.Select(t=>new {t.Id,t.Amount,t.EditVersion}).ToListAsync());
        Assert.Equal(0,await db.Context().BillPayments.CountAsync());Assert.Equal(0,await Alerts(db).CountAsync());
    }
    [Fact] public async Task Stale_settings_are_rejected_and_empty_scans_do_not_generate_notifications() {
        await using var db=await BudgetTestDatabase.CreateAsync();var s=await Auto(db).SaveAsync(1,true,0);
        await Assert.ThrowsAsync<BudgetConflictException>(()=>Auto(db).SaveAsync(1,false,0));
        Assert.True(await Auto(db).ProcessAsync(1));Assert.Equal(0,await Alerts(db).CountAsync());
        await Assert.ThrowsAsync<BudgetConflictException>(()=>Auto(db).SaveAsync(1,false,s.EditVersion));
        await Assert.ThrowsAsync<ArgumentException>(()=>Alerts(db).ListAsync(0));
    }
}
