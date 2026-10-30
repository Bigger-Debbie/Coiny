using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json.Serialization;
using Coiny.API.Controllers;
using Coiny.API.Data;
using Coiny.API.DTOs.Transactions;
using System.Text.Json;
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
using Microsoft.EntityFrameworkCore;

namespace Coiny.API.Tests;

public class TransactionImportApiTests
{
    [Fact]
    public async Task Http_review_and_save_require_auth_categories_and_duplicate_decisions_and_replay_safely()
    {
        await using var db = await BudgetTestDatabase.CreateAsync();
        var c = db.Context();
        c.Accounts.Add(new Account { Id = 1, HouseholdId = 1, Name = "Checking", OpeningBalance = 100, OpeningBalanceDate = new(2026, 10, 1) });
        c.Categories.Add(new Category { Id = 1, HouseholdId = 1, Name = "Dining", CategoryType = CategoryType.Expense, IsActive = true });
        await c.SaveChangesAsync();
        var builder = WebApplication.CreateBuilder(); builder.WebHost.UseTestServer(); builder.Logging.ClearProviders();
        builder.Services.AddControllers().AddApplicationPart(typeof(TransactionImportsController).Assembly)
            .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
        builder.Services.AddAuthentication("Test").AddScheme<AuthenticationSchemeOptions, TestAuth>("Test", _ => { });
        builder.Services.AddAuthorization(); builder.Services.AddScoped<CoinyContext>(_ => db.Context());
        builder.Services.AddScoped<ICurrentUserService, User>();
        builder.Services.AddSingleton<TimeProvider>(db.Clock);
        builder.Services.AddScoped<IAccountBalanceService, AccountBalanceService>();
        builder.Services.AddScoped<IAccountBalanceSnapshotService, AccountBalanceSnapshotService>();
        builder.Services.AddScoped<ITransactionImportService, TransactionImportService>();
        builder.Services.AddScoped<ImportUndoService>();
            builder.Services.AddScoped<Coiny.API.Data.IControlContext>(sp => sp.GetRequiredService<Coiny.API.Data.CoinyContext>());
            builder.Services.AddScoped<Coiny.API.Data.IFinancialContext>(sp => sp.GetRequiredService<Coiny.API.Data.CoinyContext>());

        await using var app = builder.Build(); app.UseAuthentication(); app.UseAuthorization(); app.MapControllers(); await app.StartAsync();
        using var client = app.GetTestClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/transaction-imports")).StatusCode);
        client.DefaultRequestHeaders.Add("Test-User", "test");
        var submission = Guid.NewGuid();
        object Payload(int? category, Guid? id = null, string direction = "Out") => new
        {
            accountId = 1, submissionId = id ?? submission, filename = "bank.csv",
            rows = new[] { new { rowId = 2, date = "2026-10-16", amount = 5, direction, categoryType = "Expense", categoryId = category, description = "Coffee" } },
        };
        var missing = await client.PostAsJsonAsync("/api/transaction-imports/validate", Payload(null));
        Assert.Equal(HttpStatusCode.OK, missing.StatusCode); Assert.Contains("\"canImport\":false", await missing.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await client.PostAsJsonAsync("/api/transaction-imports", Payload(null))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/transaction-imports/validate", Payload(1, direction: "NotADirection"))).StatusCode);
        var imported = await client.PostAsJsonAsync("/api/transaction-imports", Payload(1)); imported.EnsureSuccessStatusCode();
        var first = await imported.Content.ReadAsStringAsync(); Assert.Contains("\"importedCount\":1", first);
        var retry = await client.PostAsJsonAsync("/api/transaction-imports", Payload(1));
        Assert.Equal(first, await retry.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await client.PostAsJsonAsync("/api/transaction-imports", Payload(1, Guid.NewGuid()))).StatusCode);
        var history = await client.GetStringAsync("/api/transaction-imports?accountId=1"); Assert.Contains("bank.csv", history);
        Assert.Equal(1, await c.Transactions.CountAsync());
        var batchId=JsonDocument.Parse(first).RootElement.GetProperty("batchId").GetInt32();
        var options=new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var review=(await client.GetFromJsonAsync<ImportUndoPreview>($"/api/transaction-imports/{batchId}/review",options))!;Assert.Single(review.Rows);
        var previewResponse=await client.PostAsJsonAsync("/api/transaction-imports/undo/preview",new{batchIds=new[]{batchId},removeTransactionIds=review.RemoveTransactionIds});previewResponse.EnsureSuccessStatusCode();
        var preview=(await previewResponse.Content.ReadFromJsonAsync<ImportUndoPreview>(options))!;
        var undo=new ImportUndoRequest{BatchIds=[batchId],RemoveTransactionIds=preview.RemoveTransactionIds.ToArray(),ReviewedFingerprint=preview.Fingerprint,OperationId=Guid.NewGuid()};
        var undone=await client.PostAsJsonAsync("/api/transaction-imports/undo",undo);undone.EnsureSuccessStatusCode();
        (await client.PostAsJsonAsync("/api/transaction-imports/undo",undo)).EnsureSuccessStatusCode();
        Assert.Equal(0,await c.Transactions.CountAsync());
        client.DefaultRequestHeaders.Remove("Test-User");
        Assert.Equal(HttpStatusCode.Unauthorized,(await client.GetAsync($"/api/transaction-imports/{batchId}/review")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,(await client.PostAsJsonAsync("/api/transaction-imports/undo",undo)).StatusCode);
    }
    private sealed class User : ICurrentUserService
    {
        public string? GetUserId() => "test";
        public bool IsAuthenticated() => true;
        public Task<int> GetCurrentHouseholdIdAsync() => Task.FromResult(1);
    }
    private sealed class TestAuth(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync() => Task.FromResult(
            Request.Headers.ContainsKey("Test-User") ? AuthenticateResult.Success(new AuthenticationTicket(
                new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "test")], "Test")), "Test")) : AuthenticateResult.NoResult());
    }
}
