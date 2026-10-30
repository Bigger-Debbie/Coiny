namespace Coiny.API.Data.Hybrid;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class HouseholdStorageAttribute : Attribute;

public sealed class HouseholdRequestContext : IAsyncDisposable
{
    public HouseholdContext? Context { get; set; }
    public async ValueTask DisposeAsync() { if (Context != null) await Context.DisposeAsync(); }
}

public static class HybridRequestPipeline
{
    public static IApplicationBuilder UseInstallationMaintenance(this IApplicationBuilder app) => app.Use(async (http, next) => {
        try {
            await using var admission = await http.RequestServices.GetRequiredService<InstallationMaintenance>().EnterAsync(http.RequestAborted);
            await next(http);
        } catch (InstallationMaintenanceException) when (!http.Response.HasStarted) {
            http.Response.StatusCode = 503;
            http.Response.Headers.RetryAfter = "60";
            await http.Response.WriteAsJsonAsync(new { message = "Coiny is temporarily paused for maintenance. Please try again shortly." });
        }
    });

    public static IApplicationBuilder UseHybridStorage(this IApplicationBuilder app) => app.Use(async (http, next) => {
        if (http.GetEndpoint()?.Metadata.GetMetadata<HouseholdStorageAttribute>() != null && http.User.Identity?.IsAuthenticated == true) {
            var id = await http.RequestServices.GetRequiredService<Coiny.API.Interfaces.ICurrentUserService>().GetCurrentHouseholdIdAsync();
            var storage = http.RequestServices.GetRequiredService<HouseholdDatabaseService>();
            using var writer = HttpMethods.IsGet(http.Request.Method) || HttpMethods.IsHead(http.Request.Method)
                ? null : await storage.AcquireWriterAsync(id, http.RequestAborted);
            http.RequestServices.GetRequiredService<HouseholdRequestContext>().Context = await storage.OpenForSessionAsync(http.User, id, http.RequestAborted);
            await next(http); return;
        }
        await next(http);
    });

    public static IApplicationBuilder UseHybridFailureResponses(this IApplicationBuilder app) => app.Use(async (http, next) => {
        try { await next(http); }
        catch (Coiny.API.Services.HouseholdMembershipRequiredException) when (!http.Response.HasStarted) {
            http.Response.StatusCode = 409; await http.Response.WriteAsJsonAsync(new { message = "Set up or join a household to continue." });
        }
        catch (HouseholdStorageAccessDeniedException) when (!http.Response.HasStarted) {
            http.Response.StatusCode = 403; await http.Response.WriteAsJsonAsync(new { message = "Household access denied." });
        }
        catch (Exception error) when (!http.Response.HasStarted && (error is Npgsql.NpgsqlException or HouseholdStorageUnavailableException or Microsoft.Data.Sqlite.SqliteException or IOException)) {
            http.RequestServices.GetRequiredService<ILogger<HouseholdDatabaseService>>().LogWarning("Storage request failed ({FailureType}).", error.GetType().Name);
            http.Response.StatusCode = 503; await http.Response.WriteAsJsonAsync(new { message = "Household storage is temporarily unavailable. Please try again." });
        }
    });
}
