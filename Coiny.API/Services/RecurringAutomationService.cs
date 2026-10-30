using Coiny.API.Data;
using Coiny.API.Data.Hybrid;
using Coiny.API.Interfaces;
using Coiny.API.Models;
using Microsoft.EntityFrameworkCore;
namespace Coiny.API.Services;
public record RecurringSettingResponse(bool Enabled, DateOnly? LastScanDate, int EditVersion);
public class RecurringAutomationService(IFinancialContext context, IBudgetScheduleService schedules, TimeProvider clock)
{
    private DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.GetUtcNow(),TimeZoneInfo.FindSystemTimeZoneById("America/New_York")).DateTime);
    public async Task<RecurringSettingResponse> GetAsync(int household) {
        var s=await context.HouseholdRecurringSettings.AsNoTracking().SingleOrDefaultAsync(s=>s.HouseholdId==household);
        return new(s?.Enabled??false,s?.LastScanDate,s?.EditVersion??0);
    }
    public async Task<RecurringSettingResponse> SaveAsync(int household,bool enabled,int expectedEditVersion) {
        var s=await context.HouseholdRecurringSettings.SingleOrDefaultAsync(s=>s.HouseholdId==household);
        if((s?.EditVersion??0)!=expectedEditVersion) throw new BudgetConflictException("This household setting changed elsewhere. Reload and try again.");
        if(s==null) { s=new() {HouseholdId=household};context.Add(s); }
        s.Enabled=enabled;s.EditVersion++;
        try {await context.SaveChangesAsync();} catch(DbUpdateException) {throw new BudgetConflictException("This household setting changed elsewhere. Reload and try again.");}
        return new(s.Enabled,s.LastScanDate,s.EditVersion);
    }
    public async Task<bool> ProcessAsync(int household,CancellationToken ct=default) {
        var today=Today;
        var setting=await context.HouseholdRecurringSettings.AsNoTracking().SingleOrDefaultAsync(s=>s.HouseholdId==household,ct);
        if(setting is not {Enabled:true} || setting.LastScanDate>=today) return false;
        var worker=new HouseholdScanUser(household);
        var budgets=new BudgetService(context,worker,schedules,clock);
        var scan=await new RecurringExpenseService(context,worker,budgets,clock).ScanHouseholdAsync(household,budgets);
        ct.ThrowIfCancellationRequested();
        // Recheck inside the write transaction: disabling or a concurrent worker must win.
        await using var tx=await context.Database.BeginTransactionAsync(ct);
        var current=await context.HouseholdRecurringSettings.SingleAsync(s=>s.HouseholdId==household,ct);
        if(!current.Enabled || current.LastScanDate>=today) return false;
        var existing=await context.HouseholdAlerts.Where(a=>a.HouseholdId==household).ToDictionaryAsync(a=>a.PatternKey,ct);
        var needs=scan.Suggestions.Where(s=>s.Status=="NeedsReview").ToDictionary(s=>s.Key);
        var reviews=await context.RecurringExpenseReviews.AsNoTracking().Where(r=>r.HouseholdId==household).ToListAsync(ct);
        foreach(var review in reviews) {
            if(needs.TryGetValue(review.PatternKey,out var suggestion) &&
                (review.AllocationKey.HasValue && scan.BudgetItems.Any(b=>b.Key==review.AllocationKey) ||
                 review.Dismissed && Math.Abs(review.ReviewedAmount-suggestion.TypicalAmount)<=Math.Max(2,review.ReviewedAmount*.2m))) needs.Remove(review.PatternKey);
        }
        foreach(var alert in existing.Values.Where(a=>!needs.ContainsKey(a.PatternKey))) alert.Active=false;
        foreach(var s in needs.Values) {
            if(!existing.TryGetValue(s.Key,out var alert)) {
                alert=new() {HouseholdId=household,PatternKey=s.Key};context.Add(alert);
            } else if(!alert.Active || Math.Abs(alert.TypicalAmount-s.TypicalAmount)>Math.Max(2,alert.TypicalAmount*.2m)) alert.Version++;
            alert.Active=true;alert.Merchant=s.Merchant.Length>500?s.Merchant[..500]:s.Merchant;alert.Frequency=s.Frequency;
            alert.TypicalAmount=s.TypicalAmount;alert.NextExpected=s.NextExpected;alert.UpdatedAt=clock.GetUtcNow().UtcDateTime;
        }
        current.LastScanDate=today;current.EditVersion++;
        await context.SaveChangesAsync(ct);await tx.CommitAsync(ct);return true;
    }
    private sealed class HouseholdScanUser(int household):ICurrentUserService {
        public bool IsAuthenticated()=>true;public string? GetUserId()=>null;
        public Task<int> GetCurrentHouseholdIdAsync()=>Task.FromResult(household);
    }
}
public class RecurringScanWorker(IServiceScopeFactory scopes,ILogger<RecurringScanWorker> log, IConfiguration configuration):BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
        // No daily timer is lost on restart: persisted local scan dates decide what is due.
        while(!stoppingToken.IsCancellationRequested) {
            try {
                using var scope=scopes.CreateScope();
                var hybrid = configuration.GetValue<bool>("HybridStorage:Enabled");
                await using var admission = hybrid
                    ? await scope.ServiceProvider.GetRequiredService<InstallationMaintenance>().EnterAsync(stoppingToken) : null;
                int[] ids;
                if (hybrid) {
                    var db = scope.ServiceProvider.GetRequiredService<ControlContext>();
                    var storage = scope.ServiceProvider.GetRequiredService<HouseholdDatabaseService>();
                    await storage.RecoverProvisioningAsync(stoppingToken);
                    ids = await db.HouseholdDatabases.AsNoTracking().Where(x => x.State == HouseholdDatabaseState.Ready && x.OwnerNode == storage.Node).Select(x => x.HouseholdId).ToArrayAsync(stoppingToken);
                } else {
                    var db = scope.ServiceProvider.GetRequiredService<IFinancialContext>();
                    ids = await db.HouseholdRecurringSettings.AsNoTracking().Where(s=>s.Enabled).Select(s=>s.HouseholdId).ToArrayAsync(stoppingToken);
                }
                foreach(var id in ids) {
                    try {
                        await using var householdScope=scopes.CreateAsyncScope();
                        using var writer = hybrid ? await householdScope.ServiceProvider.GetRequiredService<HouseholdDatabaseService>().AcquireWriterAsync(id, stoppingToken) : null;
                        if (hybrid) householdScope.ServiceProvider.GetRequiredService<HouseholdRequestContext>().Context = await householdScope.ServiceProvider.GetRequiredService<HouseholdDatabaseService>().OpenForWorkerAsync(id, stoppingToken);
                        if (hybrid) {
                            var control = householdScope.ServiceProvider.GetRequiredService<ControlContext>();
                            var pending = await control.Set<PendingPreferenceChange>().Where(x => x.HouseholdId == id && x.CompletedAt == null).Select(x => x.Id).ToArrayAsync(stoppingToken);
                            foreach (var operation in pending) {
                                try { await householdScope.ServiceProvider.GetRequiredService<HybridPreferenceChanges>().CompleteAsync(operation); }
                                catch (BudgetConflictException) { /* A stale intent is durably cancelled; continue scanning. */ }
                            }
                        }
                        await householdScope.ServiceProvider.GetRequiredService<RecurringAutomationService>().ProcessAsync(id,stoppingToken);
                    }
                    catch(OperationCanceledException) when(stoppingToken.IsCancellationRequested) {return;}
                    catch(Exception e) {log.LogWarning(e,"Recurring scan failed for household {HouseholdId}; will retry.",id);}
                }
            } catch(InstallationMaintenanceException) { /* Expected pause; no writes or financial files opened. */ }
            catch(OperationCanceledException) when(stoppingToken.IsCancellationRequested) {return;}
            catch(Exception e) {log.LogWarning(e,"Recurring scan scheduler could not check households; will retry.");}
            try {await Task.Delay(TimeSpan.FromMinutes(15),stoppingToken);}catch(OperationCanceledException) {return;}
        }
    }
}
