using System.Diagnostics;
using Microsoft.AspNetCore.Routing;

namespace Coiny.API.Logging;
public static class JournalExtensions
{
    public static IServiceCollection AddOperationalJournal(this IServiceCollection services, IConfiguration config) {
        var options = new JournalOptions(); config.GetSection("Operations:Logging").Bind(options);
        services.AddSingleton(options);
        services.AddSingleton<OperationalJournal>();
        services.AddHostedService(sp => sp.GetRequiredService<OperationalJournal>());
        services.AddSingleton<ILoggerProvider, SafeJournalProvider>();
        return services;
    }
    public static IApplicationBuilder UseOperationalJournal(this IApplicationBuilder app) => app.Use(async (http, next) => {
        var journal = http.RequestServices.GetRequiredService<OperationalJournal>();
        var requestId = Activity.Current?.TraceId.ToString() ?? Guid.NewGuid().ToString("N");
        http.Response.Headers["X-Request-ID"] = requestId;
        var started = Stopwatch.GetTimestamp(); var failed = false;
        try { await next(http); }
        catch (OperationCanceledException) when (http.RequestAborted.IsCancellationRequested) { throw; }
        catch (Exception error) {
            failed = true;
            journal.Record("api", "Error", "request.failed", requestId, ("exceptionType", error.GetType().Name));
            if (http.Response.HasStarted) throw;
            http.Response.Clear(); http.Response.StatusCode = 500; http.Response.Headers["X-Request-ID"] = requestId;
            await http.Response.WriteAsJsonAsync(new { message = "The request could not be completed.", requestId });
        }
        finally {
            // Route templates, never paths or query strings; matched route values may contain secrets.
            var route = (http.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText ?? "unmatched";
            journal.Record("api", failed || http.Response.StatusCode >= 500 ? "Error" : http.Response.StatusCode >= 400 ? "Warning" : "Information",
                    "request.completed", requestId, ("method", new[] { "GET", "POST", "PUT", "PATCH", "DELETE", "HEAD", "OPTIONS" }.Contains(http.Request.Method) ? http.Request.Method : "OTHER"), ("route", route),
                    ("status", http.Response.StatusCode.ToString()), ("durationMs", Stopwatch.GetElapsedTime(started).TotalMilliseconds.ToString("F0", System.Globalization.CultureInfo.InvariantCulture)));
        }
    });
}
