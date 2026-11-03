using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Coiny.API.Logging;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Coiny.API.Tests;
public sealed class JournalRequestTests
{
    [Fact]
    public async Task RequestLogsUseRouteTemplatesAndNeverCaptureQueryValuesOrErrorMessages() {
        var builder = WebApplication.CreateBuilder(); builder.WebHost.UseTestServer(); builder.Logging.ClearProviders();
        builder.Services.AddOperationalJournal(builder.Configuration);
        await using var app = builder.Build(); app.UseOperationalJournal(); app.UseRouting();
        app.MapGet("/accounts/{id}", () => "ok");
        app.MapGet("/failure", () => Task.FromException(new InvalidOperationException("synthetic-private-error")));
        await app.StartAsync(); using var client = app.GetTestClient();
        var ok = await client.GetAsync("/accounts/synthetic-private-id?token=synthetic-private-query");
        Assert.True(ok.Headers.Contains("X-Request-ID"));
        var failure = await client.GetAsync("/failure"); Assert.Equal(HttpStatusCode.InternalServerError, failure.StatusCode);
        var body = await failure.Content.ReadAsStringAsync(); Assert.DoesNotContain("synthetic-private-error", body);
        var journal = app.Services.GetRequiredService<OperationalJournal>();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (journal.Snapshot().Count(e => e.Fields.GetValueOrDefault("event") == "request.completed") < 2) await Task.Delay(10, deadline.Token);
        var all = JsonSerializer.Serialize(journal.Snapshot());
        Assert.DoesNotContain("synthetic-private", all); Assert.Contains("/accounts/{id}", all);
        Assert.Contains(journal.Snapshot(), e => e.Fields.GetValueOrDefault("status") == "500");
        await app.StopAsync();
    }
}
