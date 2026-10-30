using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Coiny.API.Controllers;
using Coiny.API.Data;
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
public class InstitutionApiTests
{
    [Fact] public async Task Updates_require_authentication_original_values_and_household_ownership()
    {
        await using var db = await BudgetTestDatabase.CreateAsync(); var c = db.Context();
        c.Institutions.AddRange(new Institution { Id=1, HouseholdId=1, Name="Bank", Website="https://bank.example", IsActive=true }, new Institution { Id=2, HouseholdId=2, Name="Foreign", IsActive=true }); await c.SaveChangesAsync();
        var builder = WebApplication.CreateBuilder(); builder.WebHost.UseTestServer(); builder.Logging.ClearProviders();
        builder.Services.AddControllers().AddApplicationPart(typeof(InstitutionsController).Assembly);
        builder.Services.AddAuthentication("Test").AddScheme<AuthenticationSchemeOptions, TestAuth>("Test", _=>{}); builder.Services.AddAuthorization();
        builder.Services.AddScoped<CoinyContext>(_=>db.Context()); builder.Services.AddScoped<IFinancialContext>(sp=>sp.GetRequiredService<CoinyContext>()); builder.Services.AddScoped<IControlContext>(sp=>sp.GetRequiredService<CoinyContext>());
        builder.Services.AddScoped<ICurrentUserService,User>(); builder.Services.AddScoped<IInstitutionService,InstitutionService>();
        await using var app = builder.Build(); app.UseAuthentication(); app.UseAuthorization(); app.MapControllers(); await app.StartAsync(); using var client = app.GetTestClient();
        Assert.Equal(HttpStatusCode.Unauthorized,(await client.PutAsJsonAsync("/api/institutions/1",new{})).StatusCode);
        client.DefaultRequestHeaders.Add("Test-User","test");
        Assert.Equal(HttpStatusCode.BadRequest,(await client.PutAsJsonAsync("/api/institutions/1",new{name="New bank"})).StatusCode);
        var input = new {name="New bank",website=(string?)null,expectedName="Bank",expectedWebsite="https://bank.example"};
        Assert.Equal(HttpStatusCode.NotFound,(await client.PutAsJsonAsync("/api/institutions/2",input)).StatusCode);
        Assert.Equal(HttpStatusCode.OK,(await client.PutAsJsonAsync("/api/institutions/1",input)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,(await client.PutAsJsonAsync("/api/institutions/1",input)).StatusCode);
    }
    private sealed class User : ICurrentUserService { public string? GetUserId()=>"test"; public bool IsAuthenticated()=>true; public Task<int> GetCurrentHouseholdIdAsync()=>Task.FromResult(1); }
    private sealed class TestAuth(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options,logger,encoder) {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()=>Task.FromResult(Request.Headers.ContainsKey("Test-User") ? AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier,"test")],"Test")),"Test")) : AuthenticateResult.NoResult());
    }
}
