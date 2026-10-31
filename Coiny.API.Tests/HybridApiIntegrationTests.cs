using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Coiny.API.Configuration;
using Coiny.API.Controllers;
using Coiny.API.Data;
using Coiny.API.Data.Hybrid;
using Coiny.API.DTOs.Auth;
using Coiny.API.Extensions;
using Coiny.API.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Coiny.API.Tests;

public class HybridApiIntegrationTests
{
    [PostgresFact]
    public async Task Actual_endpoints_register_provision_invite_revoke_and_use_separate_financial_files()
    {
        await using var fixture = new Fixture(); await fixture.InitializeAsync(); var client = fixture.Client;
        var alice = await fixture.Register("alice"); fixture.Use(alice);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/households/me")).StatusCode);
        var household = await client.PostAsJsonAsync("/api/households", new { name = "A" }); household.EnsureSuccessStatusCode();
        var householdId = (await household.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();
        var accountResponse = await client.PostAsJsonAsync("/api/accounts", new { name = "A checking", accountType = "Checking", openingBalance = 1000, openingBalanceDate = "2026-01-01" }); accountResponse.EnsureSuccessStatusCode();
        var accountId = (await accountResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();
        var invitation = await client.PostAsJsonAsync("/api/households/invitations", new { }); invitation.EnsureSuccessStatusCode();
        var code = (await invitation.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString();
        var bob = await fixture.Register("bob"); fixture.Use(bob);
        var redeem = await client.PostAsJsonAsync("/api/households/invitations/redeem", new { code, householdId }); redeem.EnsureSuccessStatusCode();
        Assert.Single((await client.GetFromJsonAsync<JsonElement>("/api/accounts")).EnumerateArray());
        var carol = await fixture.Register("carol"); fixture.Use(carol);
        (await client.PostAsJsonAsync("/api/households", new { name = "C" })).EnsureSuccessStatusCode();
        Assert.Empty((await client.GetFromJsonAsync<JsonElement>("/api/accounts")).EnumerateArray());
        Assert.Equal(HttpStatusCode.NotFound, (await client.PutAsJsonAsync($"/api/accounts/{accountId}/loan-subtype", new { loanSubtype = "Mortgage" })).StatusCode);
        fixture.Use(alice);
        var members = await client.GetFromJsonAsync<JsonElement>("/api/households/members");
        var bobMembership = members.EnumerateArray().Single(x => x.GetProperty("email").GetString() == "bob@coiny.test").GetProperty("id").GetInt32();
        (await client.DeleteAsync($"/api/households/members/{bobMembership}")).EnsureSuccessStatusCode();
        fixture.Use(bob); Assert.Equal(HttpStatusCode.Conflict, (await client.GetAsync("/api/accounts")).StatusCode);
        fixture.Use(alice);
        (await client.PostAsJsonAsync("/api/auth/logout", new { })).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/accounts")).StatusCode);
        await using var db = await fixture.Storage.factory.CreateDbContextAsync();
        Assert.Equal(3, await db.Users.CountAsync()); Assert.Equal(2, await db.HouseholdDatabases.CountAsync());
    }

    [PostgresFact]
    public async Task Preferences_and_recovery_codes_work_through_the_real_split_services()
    {
        await using var fixture = new Fixture(); await fixture.InitializeAsync(); var client = fixture.Client;
        fixture.Use(await fixture.Register("alice"));
        (await client.PostAsJsonAsync("/api/households", new { name = "A" })).EnsureSuccessStatusCode();
        var response = await client.PutAsJsonAsync("/api/users/me/preferences", new { includeMortgageInNetWorth = false, recurringDetection = new { enabled = true, expectedEditVersion = 0 } }); response.EnsureSuccessStatusCode();
        Assert.False((await client.GetFromJsonAsync<JsonElement>("/api/users/me/preferences")).GetProperty("includeMortgageInNetWorth").GetBoolean());
        var generated = await client.PostAsJsonAsync("/api/auth/recovery-codes", new { currentPassword = "Synthetic!2026" }); generated.EnsureSuccessStatusCode();
        var code = (await generated.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("codes")[0].GetString();
        (await client.PostAsJsonAsync("/api/auth/recover-with-code", new { email = "alice@coiny.test", recoveryCode = code, newPassword = "NewSynthetic!2026" })).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/accounts")).StatusCode);
        var login = await client.PostAsJsonAsync("/api/auth/login", new { email = "alice@coiny.test", password = "NewSynthetic!2026" }); login.EnsureSuccessStatusCode();
        fixture.Use((await login.Content.ReadFromJsonAsync<AuthResponse>())!.Token);
        (await client.GetAsync("/api/accounts")).EnsureSuccessStatusCode();
        await using var db = await fixture.Storage.factory.CreateDbContextAsync();
        Assert.Empty(await db.Set<PendingPreferenceChange>().Where(x => x.CompletedAt == null).ToListAsync());
    }

    [PostgresFact]
    public async Task Maintenance_blocks_central_auth_and_financial_requests_and_recovers_without_session_loss()
    {
        await using var fixture = new Fixture(); await fixture.InitializeAsync();
        fixture.Use(await fixture.Register("maintenance-user"));
        (await fixture.Client.PostAsJsonAsync("/api/households", new { name = "A" })).EnsureSuccessStatusCode();
        var gate = await HybridMaintenanceIntegrationTests.GateAsync(fixture.Storage);
        var operation = Guid.NewGuid(); await gate.RequestAsync(operation);
        await using (var drained = await gate.DrainAsync(operation)) { }
        foreach (var response in new[] {
            await fixture.Client.GetAsync("/api/accounts"),
            await fixture.Client.PostAsJsonAsync("/api/auth/login", new { email = "maintenance-user@coiny.test", password = "Synthetic!2026" }),
            await fixture.Client.PostAsJsonAsync("/api/households/invitations", new { }) }) {
            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
            Assert.Equal(TimeSpan.FromSeconds(60), response.Headers.RetryAfter!.Delta);
            Assert.Contains("maintenance", await response.Content.ReadAsStringAsync());
        }
        await gate.ResumeAsync(operation);
        (await fixture.Client.GetAsync("/api/accounts")).EnsureSuccessStatusCode();
    }

    [PostgresFact]
    public async Task Authenticator_mfa_uses_central_identity_storage_and_requires_second_factor()
    {
        await using var fixture=new Fixture();await fixture.InitializeAsync();var client=fixture.Client;
        fixture.Use(await fixture.Register("mfa-user"));
        var setupResponse=await client.PostAsJsonAsync("/api/auth/mfa/setup",new{currentPassword="Synthetic!2026"});setupResponse.EnsureSuccessStatusCode();
        var setup=await setupResponse.Content.ReadFromJsonAsync<JsonElement>();var key=setup.GetProperty("sharedKey").GetString()!;
        var code=Coiny.API.Services.TotpAuthenticator.Code(key,DateTimeOffset.UtcNow.ToUnixTimeSeconds()/30-1);
        var enabled=await client.PostAsJsonAsync("/api/auth/mfa/enable",new{setupToken=setup.GetProperty("setupToken").GetString(),code});enabled.EnsureSuccessStatusCode();
        var result=await enabled.Content.ReadFromJsonAsync<JsonElement>();fixture.Use(result.GetProperty("token").GetString()!);
        var password=await client.PostAsJsonAsync("/api/auth/login",new{email="mfa-user@coiny.test",password="Synthetic!2026"});password.EnsureSuccessStatusCode();
        var challenge=await password.Content.ReadFromJsonAsync<JsonElement>();Assert.True(challenge.GetProperty("requiresMfa").GetBoolean());Assert.False(challenge.TryGetProperty("token",out _));
        var verify=await client.PostAsJsonAsync("/api/auth/mfa/verify",new{challenge=challenge.GetProperty("challenge").GetString(),code=result.GetProperty("recoveryCodes")[0].GetString(),recoveryCode=true});verify.EnsureSuccessStatusCode();
        fixture.Use((await verify.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString()!);(await client.GetAsync("/api/auth/me")).EnsureSuccessStatusCode();
        await using var db=await fixture.Storage.factory.CreateDbContextAsync();
        var row=await db.Set<Microsoft.AspNetCore.Identity.IdentityUserToken<string>>().SingleAsync(t=>t.LoginProvider==Coiny.API.Services.AuthenticatorMfaService.Provider);
        Assert.DoesNotContain(key,row.Value);Assert.Empty(await db.HouseholdDatabases.ToListAsync());
    }

    internal sealed class Fixture : IAsyncDisposable
    {
        public HybridStorageIntegrationTests Storage { get; } = new();
        private WebApplication app = null!;
        public HttpClient Client { get; private set; } = null!;
        public async Task InitializeAsync()
        {
            await Storage.InitializeAsync();
            await using var connectionContext = await Storage.factory.CreateDbContextAsync();
            var builder = WebApplication.CreateBuilder(); builder.WebHost.UseTestServer(); builder.Logging.ClearProviders();
            builder.Configuration["HybridStorage:Enabled"] = "true";
            builder.Configuration["HybridStorage:ControlConnection"] = connectionContext.Database.GetConnectionString();
            builder.Configuration["HybridStorage:DataRoot"] = Storage.root.DirectoryPath; builder.Configuration["HybridStorage:Node"] = Storage.root.Node;
            builder.Configuration["Jwt:Key"] = "isolated-integration-test-signing-key-at-least-32-bytes";
            builder.Configuration["Jwt:Issuer"] = "integration"; builder.Configuration["Jwt:Audience"] = "integration"; builder.Configuration["Jwt:ExpirationMinutes"] = "120";
            builder.Services.AddDatabase(builder.Configuration); builder.Services.AddIdentityServices(true);
            builder.Services.AddSingleton(Storage.root); builder.Services.AddSingleton(Storage.storage);
            builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));
            builder.Services.AddJwtAuthentication(builder.Configuration); builder.Services.AddApplicationServices(true);
            builder.Services.AddSingleton<Microsoft.AspNetCore.DataProtection.IDataProtectionProvider>(new Microsoft.AspNetCore.DataProtection.EphemeralDataProtectionProvider());
            builder.Services.AddHouseholdInvitationRateLimits(); builder.Services.AddAccountSecurityRateLimits();
            builder.Services.AddControllers().AddApplicationPart(typeof(AuthController).Assembly).AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
            app = builder.Build(); app.UseRouting(); app.UseHybridFailureResponses(); app.UseInstallationMaintenance(); app.UseAuthentication(); app.UseAuthorization(); app.UseRateLimiter(); app.UseHybridStorage(); app.MapControllers(); await app.StartAsync(); Client = app.GetTestClient();
        }
        public async Task<string> Register(string name)
        {
            var response = await Client.PostAsJsonAsync("/api/auth/register", new { firstName = name, email = name + "@coiny.test", password = "Synthetic!2026" }); response.EnsureSuccessStatusCode();
            return (await response.Content.ReadFromJsonAsync<AuthResponse>())!.Token;
        }
        public void Use(string token) => Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        public async ValueTask DisposeAsync() { Client?.Dispose(); if (app != null) await app.DisposeAsync(); await Storage.DisposeAsync(); }
    }
}
