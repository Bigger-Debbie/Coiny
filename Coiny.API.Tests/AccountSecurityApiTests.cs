using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Coiny.API.Configuration;
using Coiny.API.Controllers;
using Coiny.API.Data;
using Coiny.API.DTOs.Auth;
using Coiny.API.Extensions;
using Coiny.API.Interfaces;
using Coiny.API.Models;
using Coiny.API.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace Coiny.API.Tests;

public class AccountSecurityApiTests
{
    private const string Password = "Original-password1!";
    private const string NewPassword = "Changed-password2!";
    private const string Email = "alex@example.test";
    [Fact]
    public async Task LogoutRevokesTokenAndDoesNotAffectOtherSessions()
    {
        await using var host = await Host.Create();
        var first = await host.Register(); var second = await host.Login();
        host.Use(first); Assert.Equal(HttpStatusCode.OK, (await host.Client.GetAsync("/api/auth/me")).StatusCode);
        var sessions = await host.Client.GetFromJsonAsync<JsonElement>("/api/auth/sessions");
        Assert.Equal(2, sessions.GetArrayLength()); Assert.Single(sessions.EnumerateArray(), row => row.GetProperty("isCurrent").GetBoolean());
        (await host.Client.PostAsJsonAsync("/api/auth/logout", new {})).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Unauthorized, (await host.Client.GetAsync("/api/auth/me")).StatusCode);
        host.Use(second); Assert.Equal(HttpStatusCode.OK, (await host.Client.GetAsync("/api/auth/me")).StatusCode);
    }
    [Fact]
    public async Task SessionRevocationCannotCrossUserBoundaries()
    {
        await using var host = await Host.Create(); var first = await host.Register(); var other = await host.Register("other@example.test");
        host.Use(first); var foreignId = new JwtSecurityTokenHandler().ReadJwtToken(other).Id;
        (await host.Client.DeleteAsync("/api/auth/sessions/" + foreignId)).EnsureSuccessStatusCode();
        host.Use(other); Assert.Equal(HttpStatusCode.OK, (await host.Client.GetAsync("/api/auth/me")).StatusCode);
        host.Use(first); (await host.Client.DeleteAsync("/api/auth/sessions/" + new JwtSecurityTokenHandler().ReadJwtToken(first).Id)).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Unauthorized, (await host.Client.GetAsync("/api/auth/sessions")).StatusCode);
    }
    [Fact]
    public async Task PasswordChangeRequiresCurrentPasswordAndRevokesAllOldTokens()
    {
        await using var host = await Host.Create(); var first = await host.Register(); var second = await host.Login(); host.Use(first);
        Assert.Equal(HttpStatusCode.BadRequest, (await host.Client.PostAsJsonAsync("/api/auth/change-password", new { currentPassword = "wrong", newPassword = NewPassword })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await host.Client.PostAsJsonAsync("/api/auth/change-password", new { currentPassword = Password, newPassword = "weakpass" })).StatusCode);
        var changed = await host.Client.PostAsJsonAsync("/api/auth/change-password", new { currentPassword = Password, newPassword = NewPassword });
        changed.EnsureSuccessStatusCode();
        var renewed = (await changed.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString()!;
        Assert.NotEqual(first, renewed); Assert.NotEqual(second, renewed);
        host.Use(renewed);
        Assert.Equal(HttpStatusCode.OK, (await host.Client.GetAsync("/api/auth/me")).StatusCode);
        var active = await host.Client.GetFromJsonAsync<JsonElement>("/api/auth/sessions");
        Assert.Single(active.EnumerateArray()); Assert.True(active[0].GetProperty("isCurrent").GetBoolean());
        foreach (var token in new[] { first, second }) { host.Use(token); Assert.Equal(HttpStatusCode.Unauthorized, (await host.Client.GetAsync("/api/auth/me")).StatusCode); }
        Assert.Equal(HttpStatusCode.Unauthorized, (await host.Client.PostAsJsonAsync("/api/auth/login", new { email = Email, password = Password })).StatusCode);
        host.Use(await host.Login(password: NewPassword)); Assert.Equal(HttpStatusCode.OK, (await host.Client.GetAsync("/api/auth/me")).StatusCode);
    }
    [Fact]
    public async Task RecoveryIsGenericAndResetIsSingleUseAndRevokesSessions()
    {
        await using var host = await Host.Create(); var old = await host.Register();
        var known = await host.Client.PostAsJsonAsync("/api/auth/forgot-password", new { email = Email });
        var unknown = await host.Client.PostAsJsonAsync("/api/auth/forgot-password", new { email = "missing@example.test" });
        Assert.Equal(await known.Content.ReadAsStringAsync(), await unknown.Content.ReadAsStringAsync()); Assert.Single(host.Mail.Sent);
        Assert.DoesNotContain(host.Mail.Sent[0].Token, await known.Content.ReadAsStringAsync());
        var reset = new { email = Email, token = host.Mail.Sent[0].Token, newPassword = NewPassword };
        Assert.Equal(HttpStatusCode.BadRequest, (await host.Client.PostAsJsonAsync("/api/auth/reset-password", new { email = "other@example.test", reset.token, reset.newPassword })).StatusCode);
        (await host.Client.PostAsJsonAsync("/api/auth/reset-password", reset)).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.BadRequest, (await host.Client.PostAsJsonAsync("/api/auth/reset-password", reset)).StatusCode);
        host.Use(old); Assert.Equal(HttpStatusCode.Unauthorized, (await host.Client.GetAsync("/api/auth/me")).StatusCode);
        Assert.NotEmpty(await host.Login(password: NewPassword));
    }
    [Fact]
    public async Task RecoveryWithoutMailConfigurationIsUnavailableForEveryEmail()
    {
        await using var host = await Host.Create(); await host.Register(); host.Mail.IsConfigured = false;
        foreach (var email in new[] { Email, "missing@example.test" }) Assert.Equal(HttpStatusCode.ServiceUnavailable,
            (await host.Client.PostAsJsonAsync("/api/auth/forgot-password", new { email })).StatusCode);
        Assert.Empty(host.Mail.Sent);
    }
    [Fact]
    public async Task FailedLoginsLockExistingAccountsAndResetUnlocksIt()
    {
        await using var host = await Host.Create(); await host.Register();
        using (var scope = host.App.Services.CreateScope()) await scope.ServiceProvider.GetRequiredService<CoinyContext>().Users.ExecuteUpdateAsync(set => set.SetProperty(u => u.LockoutEnabled, false));
        for (var i = 0; i < 5; i++) Assert.Equal(HttpStatusCode.Unauthorized,
            (await host.Client.PostAsJsonAsync("/api/auth/login", new { email = Email, password = "Wrong-password1!" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await host.Client.PostAsJsonAsync("/api/auth/login", new { email = Email, password = Password })).StatusCode);
        (await host.Client.PostAsJsonAsync("/api/auth/forgot-password", new { email = Email })).EnsureSuccessStatusCode();
        (await host.Client.PostAsJsonAsync("/api/auth/reset-password", new { email = Email, token = host.Mail.Sent.Single().Token, newPassword = NewPassword })).EnsureSuccessStatusCode();
        Assert.NotEmpty(await host.Login(password: NewPassword));
    }
    [Fact]
    public async Task RateLimitsProtectMissingAccountsAndRecovery()
    {
        await using var host = await Host.Create();
        for (var i = 0; i < 10; i++) Assert.Equal(HttpStatusCode.Unauthorized,
            (await host.Client.PostAsJsonAsync("/api/auth/login", new { email = "missing@example.test", password = Password })).StatusCode);
        var rejected = await host.Client.PostAsJsonAsync("/api/auth/login", new { email = "missing@example.test", password = Password });
        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode); Assert.NotNull(rejected.Headers.RetryAfter);
        for (var i = 0; i < 3; i++) (await host.Client.PostAsJsonAsync("/api/auth/forgot-password", new { email = Email })).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.TooManyRequests, (await host.Client.PostAsJsonAsync("/api/auth/forgot-password", new { email = Email })).StatusCode);
    }
    [Fact]
    public async Task SignOutEverywhereRequiresPasswordAndInvalidatesResetLinks()
    {
        await using var host = await Host.Create(); var first = await host.Register(); var second = await host.Login(); host.Use(first);
        (await host.Client.PostAsJsonAsync("/api/auth/forgot-password", new { email = Email })).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.BadRequest, (await host.Client.PostAsJsonAsync("/api/auth/logout-all", new { currentPassword = "wrong" })).StatusCode);
        (await host.Client.PostAsJsonAsync("/api/auth/logout-all", new { currentPassword = Password })).EnsureSuccessStatusCode();
        foreach (var token in new[] { first, second }) { host.Use(token); Assert.Equal(HttpStatusCode.Unauthorized, (await host.Client.GetAsync("/api/auth/me")).StatusCode); }
        Assert.Equal(HttpStatusCode.BadRequest, (await host.Client.PostAsJsonAsync("/api/auth/reset-password", new { email = Email, token = host.Mail.Sent.Single().Token, newPassword = NewPassword })).StatusCode);
    }
    [Fact]
    public async Task ExpiredSessionsLegacyTokensTamperedTokensAndExpiredResetLinksFail()
    {
        await using var host = await Host.Create(expiredReset: true); var token = await host.Register(); host.Use(token);
        using (var scope = host.App.Services.CreateScope()) {
            var db = scope.ServiceProvider.GetRequiredService<CoinyContext>();
            await db.AuthSessions.ExecuteUpdateAsync(set => set.SetProperty(s => s.ExpiresAt, DateTime.UtcNow.AddSeconds(-1)));
            var user = await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().FindByEmailAsync(Email);
            host.Use(scope.ServiceProvider.GetRequiredService<ITokenService>().CreateToken(user!));
        }
        Assert.Equal(HttpStatusCode.Unauthorized, (await host.Client.GetAsync("/api/auth/me")).StatusCode);
        host.Use(token); Assert.Equal(HttpStatusCode.Unauthorized, (await host.Client.GetAsync("/api/auth/me")).StatusCode);
        host.Use(token[..^8] + "tampered"); Assert.Equal(HttpStatusCode.Unauthorized, (await host.Client.GetAsync("/api/auth/me")).StatusCode);
        (await host.Client.PostAsJsonAsync("/api/auth/forgot-password", new { email = Email })).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.BadRequest, (await host.Client.PostAsJsonAsync("/api/auth/reset-password", new { email = Email, token = host.Mail.Sent.Single().Token, newPassword = NewPassword })).StatusCode);
    }
    private static async Task<string[]> GenerateCodes(Host host)
    {
        var response = await host.Client.PostAsJsonAsync("/api/auth/recovery-codes", new { currentPassword = Password });
        response.EnsureSuccessStatusCode(); Assert.Contains("no-store", response.Headers.CacheControl!.ToString());
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("codes").EnumerateArray().Select(c => c.GetString()!).ToArray();
    }
    private static Task<HttpResponseMessage> Redeem(Host host, string code, string email = Email, string password = NewPassword) =>
        host.Client.PostAsJsonAsync("/api/auth/recover-with-code", new { email, recoveryCode = code, newPassword = password });

    [Fact]
    public async Task RecoveryCodeManagementRequiresPasswordAndStoresOnlyBoundHashes()
    {
        await using var host = await Host.Create();
        Assert.Equal(HttpStatusCode.Unauthorized, (await host.Client.GetAsync("/api/auth/recovery-codes")).StatusCode);
        var token = await host.Register(); host.Use(token);
        var codes = await GenerateCodes(host); Assert.Equal(10, codes.Length); Assert.Equal(10, codes.Distinct().Count());
        Assert.All(codes, code => Assert.Matches("^[A-F0-9]{8}(-[A-F0-9]{8}){3}$", code));
        using (var scope = host.App.Services.CreateScope()) {
            var stored = await scope.ServiceProvider.GetRequiredService<CoinyContext>().AuthRecoveryCodes.ToListAsync();
            Assert.Equal(10, stored.Count); Assert.All(stored, row => Assert.Equal(64, row.CodeHash.Length));
            Assert.DoesNotContain(stored, row => codes.Contains(row.CodeHash) || codes.Select(code => code.Replace("-", "")).Contains(row.CodeHash));
        }
        var status = await host.Client.GetFromJsonAsync<JsonElement>("/api/auth/recovery-codes"); Assert.Equal(10, status.GetProperty("remaining").GetInt32());
        Assert.False(status.TryGetProperty("codes", out _));
        Assert.Equal(HttpStatusCode.BadRequest, (await host.Client.PostAsJsonAsync("/api/auth/recovery-codes", new { currentPassword = "wrong" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await host.Client.PostAsJsonAsync("/api/auth/recovery-codes/revoke", new { currentPassword = "wrong" })).StatusCode);
        host.Use(await host.Register("other@example.test"));
        Assert.Equal(0, (await host.Client.GetFromJsonAsync<JsonElement>("/api/auth/recovery-codes")).GetProperty("remaining").GetInt32());
        (await host.Client.PostAsJsonAsync("/api/auth/recovery-codes/revoke", new { currentPassword = Password })).EnsureSuccessStatusCode();
        host.Use(token); Assert.Equal(10, (await host.Client.GetFromJsonAsync<JsonElement>("/api/auth/recovery-codes")).GetProperty("remaining").GetInt32());
    }
    [Fact]
    public async Task RecoveryCodesWorkWithoutSmtpAndInvalidateEveryCodeAndSession()
    {
        await using var host = await Host.Create(); var first = await host.Register(); var second = await host.Login(); host.Use(first);
        var codes = await GenerateCodes(host); host.Mail.IsConfigured = false;
        var options = await host.Client.GetFromJsonAsync<JsonElement>("/api/auth/recovery-options"); Assert.False(options.GetProperty("email").GetBoolean()); Assert.True(options.GetProperty("recoveryCode").GetBoolean());
        var formatted = "  " + codes[0].ToLowerInvariant().Replace("-", " ") + "  ";
        (await Redeem(host, formatted)).EnsureSuccessStatusCode();
        foreach (var token in new[] {first, second}) {host.Use(token); Assert.Equal(HttpStatusCode.Unauthorized, (await host.Client.GetAsync("/api/auth/me")).StatusCode);}
        Assert.Equal(HttpStatusCode.BadRequest, (await Redeem(host, codes[0])).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Redeem(host, codes[1])).StatusCode);
        host.Use(await host.Login(password: NewPassword));
        Assert.Equal(0, (await host.Client.GetFromJsonAsync<JsonElement>("/api/auth/recovery-codes")).GetProperty("remaining").GetInt32()); Assert.Empty(host.Mail.Sent);
    }
    [Fact]
    public async Task FailedRecoveryKeepsCodeAndPasswordAndCannotBeUsedForAnotherUser()
    {
        await using var host = await Host.Create(); var token = await host.Register(); host.Use(token); var codes = await GenerateCodes(host);
        Assert.Equal(HttpStatusCode.BadRequest, (await Redeem(host, codes[0], "missing@example.test")).StatusCode);
        var other = await host.Register("other@example.test");
        Assert.Equal(HttpStatusCode.BadRequest, (await Redeem(host, codes[0], "other@example.test")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Redeem(host, codes[0], password: "weakpass")).StatusCode);
        host.Use(token); Assert.Equal(HttpStatusCode.OK, (await host.Client.GetAsync("/api/auth/me")).StatusCode);
        Assert.Equal(10, (await host.Client.GetFromJsonAsync<JsonElement>("/api/auth/recovery-codes")).GetProperty("remaining").GetInt32());
        (await Redeem(host, codes[0])).EnsureSuccessStatusCode(); host.Use(other);
        Assert.Equal(HttpStatusCode.OK, (await host.Client.GetAsync("/api/auth/me")).StatusCode);
    }
    [Fact]
    public async Task ReplacementAndRevocationDisableOldCodesWithoutEndingSessions()
    {
        await using var host = await Host.Create(); var token = await host.Register(); host.Use(token);
        var original = await GenerateCodes(host); var replacement = await GenerateCodes(host);
        Assert.Empty(original.Intersect(replacement)); Assert.Equal(HttpStatusCode.BadRequest, (await Redeem(host, original[0])).StatusCode);
        (await host.Client.PostAsJsonAsync("/api/auth/recovery-codes/revoke", new {currentPassword = Password})).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.BadRequest, (await Redeem(host, replacement[0])).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await host.Client.GetAsync("/api/auth/me")).StatusCode);
    }
    [Theory]
    [InlineData("change-password")]
    [InlineData("logout-all")]
    [InlineData("reset-password")]
    public async Task OtherSecurityChangesInvalidateRecoveryCodes(string action)
    {
        await using var host = await Host.Create(); var token = await host.Register(); host.Use(token); var codes = await GenerateCodes(host);
        if (action == "reset-password") {
            (await host.Client.PostAsJsonAsync("/api/auth/forgot-password", new { email = Email })).EnsureSuccessStatusCode();
            (await host.Client.PostAsJsonAsync("/api/auth/reset-password", new { email = Email, token = host.Mail.Sent.Single().Token, newPassword = NewPassword })).EnsureSuccessStatusCode();
        } else (await host.Client.PostAsJsonAsync("/api/auth/" + action, new { currentPassword = Password, newPassword = NewPassword })).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.BadRequest, (await Redeem(host, codes[0])).StatusCode);
        host.Use(await host.Login(password: action == "logout-all" ? Password : NewPassword));
        Assert.Equal(0, (await host.Client.GetFromJsonAsync<JsonElement>("/api/auth/recovery-codes")).GetProperty("remaining").GetInt32());
    }
    [Fact]
    public async Task CodeRecoveryIsRateLimitedWithoutLockingOutTheAccount()
    {
        await using var host = await Host.Create(); var token = await host.Register(); host.Use(token);
        for (var i = 0; i < 5; i++) Assert.Equal(HttpStatusCode.BadRequest, (await Redeem(host, new string('A', 32))).StatusCode);
        var rejected = await Redeem(host, new string('A', 32)); Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode); Assert.NotNull(rejected.Headers.RetryAfter);
        Assert.NotEmpty(await host.Login());
    }
    [Fact]
    public async Task SavedCodeCanRecoverAnAccountDuringPasswordLockout()
    {
        await using var host = await Host.Create(); var token = await host.Register(); host.Use(token); var codes = await GenerateCodes(host);
        for (var i = 0; i < 5; i++) Assert.Equal(HttpStatusCode.Unauthorized,
            (await host.Client.PostAsJsonAsync("/api/auth/login", new { email = Email, password = "Wrong-password1!" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await host.Client.PostAsJsonAsync("/api/auth/login", new { email = Email, password = Password })).StatusCode);
        (await Redeem(host, codes[0])).EnsureSuccessStatusCode(); Assert.NotEmpty(await host.Login(password: NewPassword));
    }
    [Fact]
    public async Task ConcurrentRedemptionCanResetThePasswordOnlyOnce()
    {
        await using var host = await Host.Create(fileDatabase: true); var token = await host.Register(); host.Use(token); var codes = await GenerateCodes(host);
        var responses = await Task.WhenAll(Task.Run(() => Redeem(host, codes[0])), Task.Run(() => Redeem(host, codes[0])));
        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.OK);
        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.BadRequest);
        Assert.NotEmpty(await host.Login(password: NewPassword));
    }
    [Fact]
    public async Task FailedSessionRevocationRollsBackPasswordAndCodeConsumption()
    {
        await using var host = await Host.Create(); var token = await host.Register(); host.Use(token); var codes = await GenerateCodes(host);
        using (var scope = host.App.Services.CreateScope()) await scope.ServiceProvider.GetRequiredService<CoinyContext>().Database.ExecuteSqlRawAsync(
            "CREATE TRIGGER RejectSessionRevocation BEFORE UPDATE ON AuthSessions BEGIN SELECT RAISE(ABORT, 'Simulated storage failure'); END;");
        await Assert.ThrowsAnyAsync<Exception>(() => Redeem(host, codes[0]));
        Assert.Equal(HttpStatusCode.OK, (await host.Client.GetAsync("/api/auth/me")).StatusCode);
        Assert.Equal(10, (await host.Client.GetFromJsonAsync<JsonElement>("/api/auth/recovery-codes")).GetProperty("remaining").GetInt32());
        using (var scope = host.App.Services.CreateScope()) await scope.ServiceProvider.GetRequiredService<CoinyContext>().Database.ExecuteSqlRawAsync("DROP TRIGGER RejectSessionRevocation;");
        Assert.NotEmpty(await host.Login()); (await Redeem(host, codes[0])).EnsureSuccessStatusCode();
    }
    [Fact]
    public async Task FailedPasswordSessionRenewalPreservesPasswordTokensAndRecoveryCodes()
    {
        await using var host = await Host.Create(); var token = await host.Register(); host.Use(token);
        await GenerateCodes(host);
        using (var scope = host.App.Services.CreateScope()) await scope.ServiceProvider.GetRequiredService<CoinyContext>().Database.ExecuteSqlRawAsync(
            "CREATE TRIGGER RejectSessionCreation BEFORE INSERT ON AuthSessions BEGIN SELECT RAISE(ABORT, 'Simulated renewal failure'); END;");
        await Assert.ThrowsAnyAsync<Exception>(() => host.Client.PostAsJsonAsync("/api/auth/change-password", new {currentPassword=Password,newPassword=NewPassword}));
        Assert.Equal(HttpStatusCode.OK, (await host.Client.GetAsync("/api/auth/me")).StatusCode);
        Assert.Equal(10, (await host.Client.GetFromJsonAsync<JsonElement>("/api/auth/recovery-codes")).GetProperty("remaining").GetInt32());
        using (var scope = host.App.Services.CreateScope()) await scope.ServiceProvider.GetRequiredService<CoinyContext>().Database.ExecuteSqlRawAsync("DROP TRIGGER RejectSessionCreation;");
        Assert.NotEmpty(await host.Login());
        Assert.Equal(HttpStatusCode.Unauthorized, (await host.Client.PostAsJsonAsync("/api/auth/login", new {email=Email,password=NewPassword})).StatusCode);
    }
    private sealed class Mail : IPasswordRecoveryEmail {
        public bool IsConfigured { get; set; } = true;
        public List<(string Email, string Token)> Sent { get; } = new();
        public Task SendAsync(string email, string token, CancellationToken cancellationToken) { Sent.Add((email, token)); return Task.CompletedTask; }
    }
    private sealed class Host(WebApplication app, SqliteConnection connection, HttpClient client, Mail mail) : IAsyncDisposable {
        public WebApplication App => app; public HttpClient Client => client; public Mail Mail => mail;
        public void Use(string token) => client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        public async Task<string> Register(string email = Email) {
            var response = await client.PostAsJsonAsync("/api/auth/register", new { firstName = "Alex", email, password = Password }); response.EnsureSuccessStatusCode();
            return (await response.Content.ReadFromJsonAsync<AuthResponse>())!.Token;
        }
        public async Task<string> Login(string email = Email, string password = Password) {
            var response = await client.PostAsJsonAsync("/api/auth/login", new { email, password }); response.EnsureSuccessStatusCode();
            return (await response.Content.ReadFromJsonAsync<AuthResponse>())!.Token;
        }
        public static async Task<Host> Create(bool expiredReset = false, bool fileDatabase = false) {
            var connection = new SqliteConnection(fileDatabase ? "Data Source=" + Path.Combine(Path.GetTempPath(), "coiny-security-test-" + Guid.NewGuid() + ".db") : "Data Source=:memory:"); await connection.OpenAsync();
            var builder = WebApplication.CreateBuilder(); builder.WebHost.UseTestServer(); builder.Logging.ClearProviders();
            builder.Configuration["Jwt:Key"] = "test-only-account-security-key-at-least-32-bytes";
            builder.Configuration["Jwt:Issuer"] = "test"; builder.Configuration["Jwt:Audience"] = "test"; builder.Configuration["Jwt:ExpirationMinutes"] = "120";
            builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));
            builder.Services.AddDbContext<CoinyContext>(o => { if (fileDatabase) o.UseSqlite(connection.ConnectionString); else o.UseSqlite(connection); }); builder.Services.AddIdentityServices();
            if (expiredReset) builder.Services.Configure<DataProtectionTokenProviderOptions>(o => o.TokenLifespan = TimeSpan.FromSeconds(-1));
            builder.Services.AddJwtAuthentication(builder.Configuration); builder.Services.AddHouseholdInvitationRateLimits(); builder.Services.AddAccountSecurityRateLimits();
            builder.Services.AddSingleton<TimeProvider>(TimeProvider.System); builder.Services.AddScoped<ITokenService, TokenService>(); builder.Services.AddScoped<AccountSecurityService>(); builder.Services.AddScoped<RecoveryCodesService>();
            var mail = new Mail(); builder.Services.AddSingleton<IPasswordRecoveryEmail>(mail);
            builder.Services.AddControllers().AddApplicationPart(typeof(AuthController).Assembly);
            builder.Services.AddScoped<Coiny.API.Data.IControlContext>(sp => sp.GetRequiredService<Coiny.API.Data.CoinyContext>());
            builder.Services.AddScoped<Coiny.API.Data.IFinancialContext>(sp => sp.GetRequiredService<Coiny.API.Data.CoinyContext>());

            var app = builder.Build(); using (var scope = app.Services.CreateScope()) await scope.ServiceProvider.GetRequiredService<CoinyContext>().Database.EnsureCreatedAsync();
            app.UseAuthentication(); app.UseAuthorization(); app.UseRateLimiter(); app.MapControllers(); await app.StartAsync();
            return new Host(app, connection, app.GetTestClient(), mail);
        }
        public async ValueTask DisposeAsync() { client.Dispose(); await app.DisposeAsync(); var file = connection.DataSource; await connection.DisposeAsync(); if (file != ":memory:" && File.Exists(file)) File.Delete(file); }
    }
}
