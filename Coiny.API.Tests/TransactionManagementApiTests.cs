using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json.Serialization;
using Coiny.API.Controllers;
using Coiny.API.Data;
using Coiny.API.Enums;
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
public class TransactionManagementApiTests
{
    [Fact] public async Task Http_edit_and_delete_require_auth_versions_and_valid_payloads()
    {
        await using var db = await BudgetTestDatabase.CreateAsync(); var c = db.Context();
        c.Accounts.Add(new() { Id = 1, HouseholdId = 1, Name = "Checking", IsActive = true, OpeningBalanceDate = new(2026,10,1), OpeningBalance = 100 });
        c.Categories.Add(new() { Id = 1, HouseholdId = 1, Name = "Food", CategoryType = CategoryType.Expense, IsActive = true });
        c.Transactions.Add(new() { Id = 1, HouseholdId = 1, AccountId = 1, CategoryId = 1, Amount = 10, TransactionDate = new(2026,10,15), Description = "Food" });
        await c.SaveChangesAsync();
        var builder = WebApplication.CreateBuilder(); builder.WebHost.UseTestServer(); builder.Logging.ClearProviders();
        builder.Services.AddControllers().AddApplicationPart(typeof(TransactionManagementController).Assembly).AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
        builder.Services.AddAuthentication("Test").AddScheme<AuthenticationSchemeOptions, TestAuth>("Test", _ => {}); builder.Services.AddAuthorization();
        builder.Services.AddScoped<CoinyContext>(_ => db.Context()); builder.Services.AddScoped<ICurrentUserService, User>();
        builder.Services.AddScoped<IAccountBalanceSnapshotService, AccountBalanceSnapshotService>(); builder.Services.AddScoped<TransactionManagementService>();
        await using var app = builder.Build(); app.UseAuthentication(); app.UseAuthorization(); app.MapControllers(); await app.StartAsync();
        using var client = app.GetTestClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/transactions/1")).StatusCode);
        client.DefaultRequestHeaders.Add("Test-User", "test");
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/transactions/1")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync("/api/transactions/1", new {})).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.DeleteAsync("/api/transactions/1")).StatusCode);
        var payload = new { expectedEditVersion = 0, accountId = 1, categoryId = 1, categoryType = "Expense", amount = 15, date = "2026-10-16", description = "Corrected", notes = "Keep", isCleared = true };
        Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync("/api/transactions/1", payload)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PutAsJsonAsync("/api/transactions/1", payload)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.DeleteAsync("/api/transactions/1?expectedEditVersion=0")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync("/api/transactions/1?expectedEditVersion=1")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/transactions/1")).StatusCode);
    }
    private sealed class User : ICurrentUserService {
        public string? GetUserId() => "test"; public bool IsAuthenticated() => true;
        public Task<int> GetCurrentHouseholdIdAsync() => Task.FromResult(1);
    }
    private sealed class TestAuth(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder) {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync() => Task.FromResult(Request.Headers.ContainsKey("Test-User") ? AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier,"test")], "Test")), "Test")) : AuthenticateResult.NoResult());
    }
}
