using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Coiny.API.Services;
using Coiny.API.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Coiny.API.Tests;
public partial class AccountSecurityApiTests {
    private static string MfaCode(string key,int delta=0) => TotpAuthenticator.Code(key,DateTimeOffset.UtcNow.ToUnixTimeSeconds()/30+delta);
    private static async Task<JsonElement> SetupMfa(Host host) {
        var response=await host.Client.PostAsJsonAsync("/api/auth/mfa/setup",new {currentPassword=Password});response.EnsureSuccessStatusCode();
        Assert.Contains("no-store",response.Headers.CacheControl!.ToString());
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }
    private static async Task<(string Key,string[] Codes)> EnableMfa(Host host) {
        var setup=await SetupMfa(host);var key=setup.GetProperty("sharedKey").GetString()!;
        var response=await host.Client.PostAsJsonAsync("/api/auth/mfa/enable",new {setupToken=setup.GetProperty("setupToken").GetString(),code=MfaCode(key,-1)});
        response.EnsureSuccessStatusCode();var result=await response.Content.ReadFromJsonAsync<JsonElement>();host.Use(result.GetProperty("token").GetString()!);
        return (key,result.GetProperty("recoveryCodes").EnumerateArray().Select(c=>c.GetString()!).ToArray());
    }
    private static async Task<string> MfaChallenge(Host host,string password=Password) {
        var response=await host.Client.PostAsJsonAsync("/api/auth/login",new {email=Email,password});response.EnsureSuccessStatusCode();
        var result=await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(result.GetProperty("requiresMfa").GetBoolean());Assert.False(result.TryGetProperty("token",out _));
        return result.GetProperty("challenge").GetString()!;
    }
    private static Task<HttpResponseMessage> VerifyMfa(Host host,string challenge,string code,bool recoveryCode=false) => host.Client.PostAsJsonAsync("/api/auth/mfa/verify",new{challenge,code,recoveryCode});

    [Fact] public async Task MfaSetupIsPasswordProtectedAndBoundToUserAndDoesNotEnableUntilConfirmed() {
        await using var host=await Host.Create();
        Assert.Equal(HttpStatusCode.Unauthorized,(await host.Client.GetAsync("/api/auth/mfa")).StatusCode);
        var token=await host.Register();host.Use(token);
        Assert.Equal(HttpStatusCode.BadRequest,(await host.Client.PostAsJsonAsync("/api/auth/mfa/setup",new{currentPassword="wrong"})).StatusCode);
        var setup=await SetupMfa(host);
        Assert.StartsWith("data:image/png;base64,",setup.GetProperty("qrCodeDataUrl").GetString());
        Assert.False((await host.Client.GetFromJsonAsync<JsonElement>("/api/auth/mfa")).GetProperty("enabled").GetBoolean());
        Assert.NotEmpty(await host.Login());
        var other=await host.Register("another@example.test");host.Use(other);
        Assert.Equal(HttpStatusCode.BadRequest,(await host.Client.PostAsJsonAsync("/api/auth/mfa/enable",new{setupToken=setup.GetProperty("setupToken").GetString(),code=MfaCode(setup.GetProperty("sharedKey").GetString()!)})).StatusCode);
        host.Use(token);var expired=setup.GetProperty("setupToken").GetString()!+"tampered";
        Assert.Equal(HttpStatusCode.BadRequest,(await host.Client.PostAsJsonAsync("/api/auth/mfa/enable",new{setupToken=expired,code="123456"})).StatusCode);
    }
    [Fact] public async Task MfaLoginRequiresSecondFactorEncryptsSecretsAndPreventsChallengeAndCodeReplay() {
        await using var host=await Host.Create();var old=await host.Register();host.Use(old);var (key,codes)=await EnableMfa(host);
        Assert.Equal(10,codes.Length);Assert.Equal(10,codes.Distinct().Count());
        var current=host.Client.DefaultRequestHeaders.Authorization!.Parameter!;
        host.Use(old);Assert.Equal(HttpStatusCode.Unauthorized,(await host.Client.GetAsync("/api/auth/me")).StatusCode);host.Use(current);
        using(var scope=host.App.Services.CreateScope()) {
            var db=scope.ServiceProvider.GetRequiredService<CoinyContext>();
            var stored=await db.Set<IdentityUserToken<string>>().SingleAsync(t=>t.LoginProvider==AuthenticatorMfaService.Provider);
            Assert.DoesNotContain(key,stored.Value);Assert.All(codes,c=>Assert.DoesNotContain(c.Replace("-",""),stored.Value));
            Assert.True((await db.Users.SingleAsync()).TwoFactorEnabled);
        }
        var challenge=await MfaChallenge(host);var forged=await VerifyMfa(host,challenge+"broken",MfaCode(key));Assert.Equal(HttpStatusCode.Unauthorized,forged.StatusCode);
        var good=await VerifyMfa(host,challenge,MfaCode(key));good.EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Unauthorized,(await VerifyMfa(host,challenge,codes[0],true)).StatusCode);
        var next=await MfaChallenge(host);Assert.Equal(HttpStatusCode.Unauthorized,(await VerifyMfa(host,next,MfaCode(key))).StatusCode);
        (await VerifyMfa(host,next,codes[0],true)).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Unauthorized,(await VerifyMfa(host,await MfaChallenge(host),codes[0],true)).StatusCode);
    }
    [Fact] public async Task MfaFailureLockoutCannotBeResetByKnowingThePassword() {
        await using var host=await Host.Create();host.Use(await host.Register());await EnableMfa(host);
        for(var i=0;i<5;i++) Assert.Equal(HttpStatusCode.Unauthorized,(await VerifyMfa(host,await MfaChallenge(host),new string('A',32),true)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,(await host.Client.PostAsJsonAsync("/api/auth/login",new{email=Email,password=Password})).StatusCode);
    }
    [Fact] public async Task PasswordResetKeepsMfaAndInvalidatesExistingChallenges() {
        await using var host=await Host.Create();host.Use(await host.Register());var (key,codes)=await EnableMfa(host);var challenge=await MfaChallenge(host);
        (await host.Client.PostAsJsonAsync("/api/auth/forgot-password",new{email=Email})).EnsureSuccessStatusCode();
        (await host.Client.PostAsJsonAsync("/api/auth/reset-password",new{email=Email,token=host.Mail.Sent.Single().Token,newPassword=NewPassword})).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Unauthorized,(await VerifyMfa(host,challenge,MfaCode(key))).StatusCode);
        (await VerifyMfa(host,await MfaChallenge(host,NewPassword),codes[0],true)).EnsureSuccessStatusCode();
    }
    [Fact] public async Task MfaManagementRequiresPasswordAndFactorAndReplacesCodesWithoutLoggingOutCurrentBrowser() {
        await using var host=await Host.Create();host.Use(await host.Register());var (key,codes)=await EnableMfa(host);
        Assert.Equal(HttpStatusCode.BadRequest,(await host.Client.PostAsJsonAsync("/api/auth/mfa/disable",new{currentPassword=Password,code=new string('B',32),recoveryCode=true})).StatusCode);
        var pending=await MfaChallenge(host);
        var replaced=await host.Client.PostAsJsonAsync("/api/auth/mfa/recovery-codes",new{currentPassword=Password,code=codes[0],recoveryCode=true});replaced.EnsureSuccessStatusCode();
        var result=await replaced.Content.ReadFromJsonAsync<JsonElement>();host.Use(result.GetProperty("token").GetString()!);
        (await host.Client.GetAsync("/api/auth/me")).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Unauthorized,(await VerifyMfa(host,pending,codes[1],true)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,(await VerifyMfa(host,await MfaChallenge(host),codes[1],true)).StatusCode);
        var disabled=await host.Client.PostAsJsonAsync("/api/auth/mfa/disable",new{currentPassword=Password,code=result.GetProperty("recoveryCodes")[0].GetString(),recoveryCode=true});disabled.EnsureSuccessStatusCode();
        result=await disabled.Content.ReadFromJsonAsync<JsonElement>();host.Use(result.GetProperty("token").GetString()!);
        Assert.False((await host.Client.GetFromJsonAsync<JsonElement>("/api/auth/mfa")).GetProperty("enabled").GetBoolean());Assert.NotEmpty(await host.Login());
    }
    [Fact] public async Task MfaLoginRollbackDoesNotConsumeBackupCodeOnSessionFailure() {
        await using var host=await Host.Create();host.Use(await host.Register());var (_,codes)=await EnableMfa(host);var challenge=await MfaChallenge(host);
        using(var scope=host.App.Services.CreateScope())await scope.ServiceProvider.GetRequiredService<CoinyContext>().Database.ExecuteSqlRawAsync("CREATE TRIGGER RejectMfaSession BEFORE INSERT ON AuthSessions BEGIN SELECT RAISE(ABORT, 'Simulated failure'); END;");
        await Assert.ThrowsAnyAsync<Exception>(()=>VerifyMfa(host,challenge,codes[0],true));
        using(var scope=host.App.Services.CreateScope())await scope.ServiceProvider.GetRequiredService<CoinyContext>().Database.ExecuteSqlRawAsync("DROP TRIGGER RejectMfaSession;");
        (await VerifyMfa(host,challenge,codes[0],true)).EnsureSuccessStatusCode();
    }
    [Fact] public async Task MfaBackupCodeConcurrentRedemptionCreatesOnlyOneSession() {
        await using var host=await Host.Create(fileDatabase:true);host.Use(await host.Register());var (_,codes)=await EnableMfa(host);var challenge=await MfaChallenge(host);
        var replies=await Task.WhenAll(Task.Run(()=>VerifyMfa(host,challenge,codes[0],true)),Task.Run(()=>VerifyMfa(host,challenge,codes[0],true)));
        Assert.Single(replies,r=>r.StatusCode==HttpStatusCode.OK);Assert.Single(replies,r=>r.StatusCode==HttpStatusCode.Unauthorized);
    }
    [Fact] public async Task ExpiredSetupAndLoginChallengesCannotBeRedeemed() {
        await using var host=await Host.Create();host.Use(await host.Register());var setup=await SetupMfa(host);
        using var scope=host.App.Services.CreateScope();var protection=scope.ServiceProvider.GetRequiredService<IDataProtectionProvider>();
        var setupProtector=protection.CreateProtector("Coiny.UserMfa.Setup.v1").ToTimeLimitedDataProtector();
        var expiredSetup=setupProtector.Protect(setupProtector.Unprotect(setup.GetProperty("setupToken").GetString()!),TimeSpan.FromSeconds(-1));
        Assert.Equal(HttpStatusCode.BadRequest,(await host.Client.PostAsJsonAsync("/api/auth/mfa/enable",new{setupToken=expiredSetup,code=MfaCode(setup.GetProperty("sharedKey").GetString()!)})).StatusCode);
        var (_,codes)=await EnableMfa(host);var challenge=await MfaChallenge(host);
        var loginProtector=protection.CreateProtector("Coiny.UserMfa.Login.v1").ToTimeLimitedDataProtector();
        var expiredLogin=loginProtector.Protect(loginProtector.Unprotect(challenge),TimeSpan.FromSeconds(-1));
        Assert.Equal(HttpStatusCode.Unauthorized,(await VerifyMfa(host,expiredLogin,codes[0],true)).StatusCode);
        (await VerifyMfa(host,challenge,codes[0],true)).EnsureSuccessStatusCode();
    }

    [Fact] public async Task MissingMfaStorageCannotDowngradeAnEnabledAccountToPasswordOnly() {
        await using var host=await Host.Create();host.Use(await host.Register());await EnableMfa(host);
        using(var scope=host.App.Services.CreateScope()) await scope.ServiceProvider.GetRequiredService<CoinyContext>().Set<IdentityUserToken<string>>()
            .Where(t=>t.LoginProvider==AuthenticatorMfaService.Provider).ExecuteDeleteAsync();
        Assert.True((await host.Client.GetFromJsonAsync<JsonElement>("/api/auth/mfa")).GetProperty("enabled").GetBoolean());
        Assert.Equal(HttpStatusCode.ServiceUnavailable,(await host.Client.PostAsJsonAsync("/api/auth/login",new{email=Email,password=Password})).StatusCode);
    }

}
