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
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
namespace Coiny.API.Tests;
public class PreferenceApiTests
{
    [Fact] public async Task Preferences_are_private_and_loan_classification_is_household_scoped()
    {
        await using var db = await BudgetTestDatabase.CreateAsync(); var c = db.Context();
        c.Categories.AddRange(new Category { Id = 20, HouseholdId = 1, Name = "Food", CategoryType = CategoryType.Expense, IsActive = true },
            new Category { Id = 21, HouseholdId = 2, Name = "Foreign", CategoryType = CategoryType.Expense, IsActive = true },
            new Category { Id = 22, HouseholdId = 1, Name = "System", CategoryType = CategoryType.Transfer, IsSystem = true, IsActive = true },
            new Category { Id = 23, HouseholdId = 1, Name = "Rent", CategoryType = CategoryType.Expense, IsActive = true });
        c.Users.AddRange(new ApplicationUser { Id = "one" }, new ApplicationUser { Id = "two" });
        c.Accounts.AddRange(new Account { Id = 1, HouseholdId = 1, Name = "Loan", AccountType = AccountType.Loan }, new Account { Id = 2, HouseholdId = 2, Name = "Foreign", AccountType = AccountType.Loan }, new Account { Id = 3, HouseholdId = 1, Name = "Checking" }); await c.SaveChangesAsync();
        var builder = WebApplication.CreateBuilder(); builder.WebHost.UseTestServer(); builder.Logging.ClearProviders();
        builder.Services.AddControllers().AddApplicationPart(typeof(UserPreferencesController).Assembly).AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
        builder.Services.AddAuthentication("Test").AddScheme<AuthenticationSchemeOptions, TestAuth>("Test", _ => {}); builder.Services.AddAuthorization(); builder.Services.AddHttpContextAccessor();
        builder.Services.AddScoped<CoinyContext>(_ => db.Context()); builder.Services.AddScoped<ICurrentUserService, User>(); builder.Services.AddScoped<UserPreferencesService>(); builder.Services.AddScoped<ICategoryService, CategoryService>();
        await using var app = builder.Build(); app.UseAuthentication(); app.UseAuthorization(); app.MapControllers(); await app.StartAsync(); using var client = app.GetTestClient();
        const string path = "/api/users/me/preferences";
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(path)).StatusCode);
        client.DefaultRequestHeaders.Add("Test-User", "one");
        Assert.True((await client.GetFromJsonAsync<UserPreferences>(path))!.IncludeMortgageInNetWorth);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PutAsJsonAsync("/api/categories/21", new { name = "Other", isActive = false })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync("/api/categories/22", new { name = "Other", isActive = false })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync("/api/categories/20", new { name = " rent ", isActive = false })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync("/api/categories/20", new { name = " " , isActive = false })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync("/api/categories/20", new { name = "Food" })).StatusCode);
        (await client.PutAsJsonAsync("/api/categories/20", new { name = " Groceries ", isActive = false, categoryType = "Income" })).EnsureSuccessStatusCode();
        using (var check = db.Context()) {
            var category = await check.Categories.FindAsync(20);
            Assert.Equal("Groceries", category!.Name); Assert.False(category.IsActive); Assert.Equal(CategoryType.Expense, category.CategoryType);
        }
        (await client.PutAsJsonAsync("/api/categories/20", new { name = "Groceries", isActive = true })).EnsureSuccessStatusCode();

        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync(path, new {})).StatusCode);
        (await client.PutAsJsonAsync(path,new { includeMortgageInNetWorth = false })).EnsureSuccessStatusCode();
        Assert.False((await client.GetFromJsonAsync<UserPreferences>(path))!.IncludeMortgageInNetWorth);
        client.DefaultRequestHeaders.Remove("Test-User"); client.DefaultRequestHeaders.Add("Test-User","two");
        Assert.True((await client.GetFromJsonAsync<UserPreferences>(path))!.IncludeMortgageInNetWorth);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PutAsJsonAsync("/api/accounts/1/loan-subtype", new { loanSubtype = "Mortgage" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PutAsJsonAsync("/api/accounts/2/loan-subtype", new { loanSubtype = "Mortgage" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync("/api/accounts/3/loan-subtype", new { loanSubtype = "Mortgage" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync("/api/accounts/1/loan-subtype", new { loanSubtype = "Invalid" })).StatusCode);
    }
    private sealed class User(IHttpContextAccessor accessor) : ICurrentUserService {
        public string? GetUserId() => accessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);
        public bool IsAuthenticated() => true; public Task<int> GetCurrentHouseholdIdAsync() => Task.FromResult(1);
    }
    private sealed class TestAuth(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder) {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync() => Task.FromResult(Request.Headers.TryGetValue("Test-User",out var id) ? AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier,id.ToString())], "Test")), "Test")) : AuthenticateResult.NoResult());
    }
}
