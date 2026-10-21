using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Coiny.API.Controllers;
using Coiny.API.Data;
using Coiny.API.DTOs.Accounts;
using Coiny.API.Interfaces;
using Coiny.API.Models;
using Coiny.API.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Coiny.API.Tests;

public class AccountReconciliationApiTests
{
    [Fact]
    public async Task Http_requires_auth_ownership_required_values_matching_balance_and_reviewed_fingerprint()
    {
        await using var db = await BudgetTestDatabase.CreateAsync(); var c = db.Context();
        c.Users.Add(new() { Id = "test", FirstName = "Alex" });
        c.Accounts.AddRange(new Account { Id = 1, HouseholdId = 1, Name = "Checking", OpeningBalance = 100, OpeningBalanceDate = new(2026, 10, 1) },
            new Account { Id = 2, HouseholdId = 2, Name = "Foreign", OpeningBalanceDate = new(2026, 10, 1) });
        await c.SaveChangesAsync();
        var builder = WebApplication.CreateBuilder(); builder.WebHost.UseTestServer(); builder.Logging.ClearProviders();
        builder.Services.AddControllers().AddApplicationPart(typeof(AccountReconciliationsController).Assembly);
        builder.Services.AddAuthentication("Test").AddScheme<AuthenticationSchemeOptions, TestAuth>("Test", _ => { }); builder.Services.AddAuthorization();
        builder.Services.AddScoped<CoinyContext>(_ => db.Context()); builder.Services.AddScoped<ICurrentUserService, User>();
        builder.Services.AddSingleton<TimeProvider>(db.Clock); builder.Services.AddScoped<AccountReconciliationService>();
        await using var app = builder.Build(); app.UseAuthentication(); app.UseAuthorization(); app.MapControllers(); await app.StartAsync(); using var client = app.GetTestClient();
        const string url = "/api/accounts/1/reconciliations";
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(url)).StatusCode);
        client.DefaultRequestHeaders.Add("Test-User", "test");
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/accounts/2/reconciliations")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(url + "/check", new { statementDate = "2026-10-05" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(url + "/check", new { statementBalance = 100 })).StatusCode);
        var check = await (await client.PostAsJsonAsync(url + "/check", new { statementDate = "2026-10-05", statementBalance = 100 })).Content.ReadFromJsonAsync<ReconciliationCheck>();
        Assert.NotNull(check); Assert.True(check.Matches);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(url, new { check.StatementDate, check.StatementBalance, reviewedFingerprint = check.LedgerFingerprint })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(url, new { check.StatementDate, check.StatementBalance, reviewedFingerprint = check.LedgerFingerprint, transactionsReviewed = false })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync(url, new { check.StatementDate, check.StatementBalance, reviewedFingerprint = new string('b', 64), transactionsReviewed = true })).StatusCode);
        (await client.PostAsJsonAsync(url, new { check.StatementDate, check.StatementBalance, reviewedFingerprint = check.LedgerFingerprint, transactionsReviewed = true })).EnsureSuccessStatusCode();
        var history = await client.GetFromJsonAsync<List<AccountReconciliationResponse>>(url); Assert.False(Assert.Single(history!).NeedsReview);
        var wrong = await (await client.PostAsJsonAsync(url + "/check", new { statementDate = "2026-10-06", statementBalance = 90 })).Content.ReadFromJsonAsync<ReconciliationCheck>();
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(url, new { wrong!.StatementDate, wrong.StatementBalance, reviewedFingerprint = wrong.LedgerFingerprint, transactionsReviewed = true })).StatusCode);
    }
    private sealed class User : ICurrentUserService
    {
        public string? GetUserId() => "test"; public bool IsAuthenticated() => true;
        public Task<int> GetCurrentHouseholdIdAsync() => Task.FromResult(1);
    }
    private sealed class TestAuth(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync() => Task.FromResult(Request.Headers.ContainsKey("Test-User") ? AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "test")], "Test")), "Test")) : AuthenticateResult.NoResult());
    }
}
