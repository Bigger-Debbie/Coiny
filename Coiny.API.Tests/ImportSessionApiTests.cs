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
using Microsoft.EntityFrameworkCore;

namespace Coiny.API.Tests;

public class ImportSessionApiTests
{
    [Fact] public async Task Session_routes_require_auth_validate_each_file_and_replay_commits()
    {
        await using var db=await BudgetTestDatabase.CreateAsync();var c=db.Context();
        c.Accounts.AddRange(new Account{Id=1,HouseholdId=1,Name="Bills",OpeningBalance=100,OpeningBalanceDate=new(2026,10,1)},new Account{Id=2,HouseholdId=1,Name="Spending",OpeningBalanceDate=new(2026,10,1)});
        c.Categories.Add(new Category{Id=1,HouseholdId=1,Name="Food",CategoryType=CategoryType.Expense,IsActive=true});await c.SaveChangesAsync();
        var builder=WebApplication.CreateBuilder();builder.WebHost.UseTestServer();builder.Logging.ClearProviders();
        builder.Services.AddControllers().AddApplicationPart(typeof(ImportSessionsController).Assembly).AddJsonOptions(o=>o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
        builder.Services.AddAuthentication("Test").AddScheme<AuthenticationSchemeOptions,TestAuth>("Test",_=>{});builder.Services.AddAuthorization();
        builder.Services.AddScoped<CoinyContext>(_=>db.Context());builder.Services.AddScoped<ICurrentUserService,User>();builder.Services.AddScoped<IAccountBalanceService,AccountBalanceService>();builder.Services.AddScoped<IAccountBalanceSnapshotService,AccountBalanceSnapshotService>();
        builder.Services.AddScoped<ITransactionImportService,TransactionImportService>();builder.Services.AddScoped<ITransferService,TransferService>();builder.Services.AddScoped<ImportSessionService>();builder.Services.AddSingleton<TimeProvider>(new TestClock());
        await using var app=builder.Build();app.UseAuthentication();app.UseAuthorization();app.MapControllers();await app.StartAsync();using var client=app.GetTestClient();
        const string path="/api/transaction-imports/sessions";
        Assert.Equal(HttpStatusCode.Unauthorized,(await client.PostAsJsonAsync(path,new{})).StatusCode);
        client.DefaultRequestHeaders.Add("Test-User","test");Assert.Equal(HttpStatusCode.BadRequest,(await client.PostAsJsonAsync(path,new{})).StatusCode);
        var payload=new Coiny.API.DTOs.Transactions.ImportSessionRequest{SubmissionId=Guid.NewGuid(),Files=[new(){AccountId=1,SubmissionId=Guid.NewGuid(),Filename="bank.csv",Rows=[new(){RowId=2,Date=new(2026,10,2),Amount=10,CategoryType=CategoryType.Expense,CategoryId=1,Direction=TransferDirection.Out,Description="Food"}]}]};
        (await client.PostAsJsonAsync(path+"/validate",payload)).EnsureSuccessStatusCode();
        var response=await client.PostAsJsonAsync(path,payload);response.EnsureSuccessStatusCode();var first=await response.Content.ReadAsStringAsync();
        Assert.Equal(first,await (await client.PostAsJsonAsync(path,payload)).Content.ReadAsStringAsync());
        payload.Links.Add(new(new(1,2,null,null),new(2,2,null,null)));Assert.Equal(HttpStatusCode.Conflict,(await client.PostAsJsonAsync(path,payload)).StatusCode);
        payload.Links.Clear();payload.SubmissionId=Guid.NewGuid();payload.Files[0].SubmissionId=Guid.NewGuid();
        Assert.Equal(HttpStatusCode.UnprocessableEntity,(await client.PostAsJsonAsync(path,payload)).StatusCode);
        Assert.Single(await db.Context().Transactions.ToListAsync());
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
