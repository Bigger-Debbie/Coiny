using Microsoft.EntityFrameworkCore;
namespace Coiny.API.Data.Hybrid;

internal static class RestoredControlSecurity
{
    // Run offline after a full central DB restore, before exposing the API. A
    // financial-only restore must never touch these central records.
    internal static async Task InvalidateHistoricalCredentialsAsync(ControlContext control)
    {
        await using var tx = await control.Database.BeginTransactionAsync();
        var now = DateTime.UtcNow;
        await control.AuthSessions.ExecuteUpdateAsync(x => x.SetProperty(s => s.RevokedAt, now));
        await control.AuthRecoveryCodes.ExecuteDeleteAsync();
        foreach (var user in await control.Users.ToListAsync()) user.SecurityStamp = Guid.NewGuid().ToString();
        await control.SaveChangesAsync(); await tx.CommitAsync();
    }
}
