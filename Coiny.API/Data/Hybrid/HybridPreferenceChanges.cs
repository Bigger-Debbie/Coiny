using Coiny.API.Services;
using Microsoft.EntityFrameworkCore;
namespace Coiny.API.Data.Hybrid;

// Durable intent plus local receipt: retries after either commit never apply the
// recurring setting twice. This is a recoverable workflow, not a distributed tx.
public sealed class HybridPreferenceChanges(IControlContext control, IFinancialContext finance, TimeProvider clock)
{
    public async Task SaveAsync(string userId, int householdId, bool includeMortgage, bool enabled, int expectedVersion)
    {
        var pending = await control.Set<PendingPreferenceChange>().SingleOrDefaultAsync(x => x.UserId == userId && x.CompletedAt == null);
        if (pending != null) {
            await CompleteAsync(pending.Id);
            throw new BudgetConflictException("Your previous settings change has finished. Reload settings before saving again.");
        }
        var change = new PendingPreferenceChange { UserId = userId, HouseholdId = householdId, IncludeMortgage = includeMortgage, ScanEnabled = enabled, ExpectedVersion = expectedVersion };
        control.Add(change);
        try { await control.SaveChangesAsync(); }
        catch (DbUpdateException error) when (DatabaseConflicts.IsUniqueViolation(error)) { throw new BudgetConflictException("Another settings change is in progress. Reload and try again."); }
        await CompleteAsync(change.Id);
    }

    public async Task CompleteAsync(Guid id)
    {
        var change = await control.Set<PendingPreferenceChange>().SingleAsync(x => x.Id == id);
        if (change.CompletedAt != null) return;
        if (!await control.HouseholdMembers.AnyAsync(x => x.HouseholdId == change.HouseholdId && x.UserId == change.UserId)) {
            change.Cancelled = true; change.CompletedAt = clock.GetUtcNow().UtcDateTime; await control.SaveChangesAsync(); return;
        }
        try {
            await using var local = await finance.Database.BeginTransactionAsync();
            if (!await finance.Set<PreferenceChangeReceipt>().AnyAsync(x => x.Id == id)) {
                await new RecurringAutomationService(finance, new BudgetScheduleService(), clock).SaveAsync(change.HouseholdId, change.ScanEnabled, change.ExpectedVersion);
                finance.Add(new PreferenceChangeReceipt { Id = id }); await finance.SaveChangesAsync();
            }
            await local.CommitAsync();
        } catch (BudgetConflictException) {
            change.Cancelled = true; change.CompletedAt = clock.GetUtcNow().UtcDateTime; await control.SaveChangesAsync(); throw;
        }
        await using var central = await control.Database.BeginTransactionAsync();
        var user = await control.Users.SingleAsync(x => x.Id == change.UserId);
        user.IncludeMortgageInNetWorth = change.IncludeMortgage;
        change.CompletedAt = clock.GetUtcNow().UtcDateTime;
        await control.SaveChangesAsync(); await central.CommitAsync();
    }
}
