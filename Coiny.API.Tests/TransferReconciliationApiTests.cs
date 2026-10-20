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
public class TransferReconciliationApiTests
{
    [Fact] public async Task Http_reconciliation_requires_auth_and_versions_and_never_creates_entries_when_linking()
    {
        await using var db = await BudgetTestDatabase.CreateAsync(); var c = db.Context();
        c.Accounts.AddRange(new Account { Id = 1, HouseholdId = 1, Name = "Bills", OpeningBalanceDate = new(2026,10,1) },
            new Account { Id = 2, HouseholdId = 1, Name = "Spending", OpeningBalanceDate = new(2026,10,1) },
            new Account { Id = 3, HouseholdId = 1, Name = "Savings", OpeningBalanceDate = new(2026,10,1) },
            new Account { Id = 4, HouseholdId = 2, Name = "Foreign", OpeningBalanceDate = new(2026,10,1) });
        c.Categories.Add(new() { Id = 1, HouseholdId = 1, Name = "Transfer", CategoryType = CategoryType.Transfer, IsSystem = true, IsActive = true });
        c.Transactions.AddRange(new Transaction { Id = 1, HouseholdId = 1, AccountId = 1, CategoryId = 1, Amount = 100, TransactionDate = new(2026,10,10), Description = "Out", TransferDirection = TransferDirection.Out, TransferCounterparty = TransferCounterparty.Unknown },
            new Transaction { Id = 2, HouseholdId = 1, AccountId = 2, CategoryId = 1, Amount = 100, TransactionDate = new(2026,10,11), Description = "In", TransferDirection = TransferDirection.In, TransferCounterparty = TransferCounterparty.Unknown }); await c.SaveChangesAsync();
        var builder = WebApplication.CreateBuilder(); builder.WebHost.UseTestServer(); builder.Logging.ClearProviders();
        builder.Services.AddControllers().AddApplicationPart(typeof(TransfersController).Assembly).AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
        builder.Services.AddAuthentication("Test").AddScheme<AuthenticationSchemeOptions, TestAuth>("Test", _ => {}); builder.Services.AddAuthorization();
        builder.Services.AddScoped<CoinyContext>(_ => db.Context()); builder.Services.AddScoped<ICurrentUserService, User>();
        builder.Services.AddScoped<IAccountBalanceSnapshotService, AccountBalanceSnapshotService>(); builder.Services.AddScoped<ITransferService, TransferService>();
        await using var app = builder.Build(); app.UseAuthentication(); app.UseAuthorization(); app.MapControllers(); await app.StartAsync(); using var client = app.GetTestClient();
        const string candidates = "/api/transfers/entries/1/candidates?accountId=2";
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(candidates)).StatusCode);
        client.DefaultRequestHeaders.Add("Test-User", "test");
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(candidates)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/transfers/entries/1/candidates?accountId=4")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync(candidates + "&days=-1")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/transfers/entries/1/counterpart", new { accountId = 3, date = "2026-10-10" })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/api/transfers/links", new { firstTransactionId = 1, secondTransactionId = 2, expectedFirstEditVersion = 99, expectedSecondEditVersion = 0 })).StatusCode);
        (await client.PostAsJsonAsync("/api/transfers/links", new { firstTransactionId = 1, secondTransactionId = 2, expectedFirstEditVersion = 0, expectedSecondEditVersion = 0 })).EnsureSuccessStatusCode();
        Assert.Equal(2, await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.CountAsync(db.Context().Transactions));
        Assert.Equal(HttpStatusCode.Conflict, (await client.GetAsync(candidates)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.DeleteAsync("/api/transfers/links/1?expectedEditVersion=0&expectedPairedEditVersion=1")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync("/api/transfers/links/1?expectedEditVersion=1&expectedPairedEditVersion=1")).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/transfers/entries/1/counterpart", new { accountId = 3, date = "2026-10-10", expectedEditVersion = 2 })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/api/transfers/entries/1/counterpart", new { accountId = 3, date = "2026-10-10", expectedEditVersion = 2 })).StatusCode);
        Assert.Equal(3, await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.CountAsync(db.Context().Transactions));
    }
    private sealed class User : ICurrentUserService {
        public string? GetUserId() => "test"; public bool IsAuthenticated() => true;
        public Task<int> GetCurrentHouseholdIdAsync() => Task.FromResult(1);
    }
    private sealed class TestAuth(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder) {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync() => Task.FromResult(Request.Headers.ContainsKey("Test-User") ? AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier,"test")], "Test")), "Test")) : AuthenticateResult.NoResult());
    }
}
