using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Coiny.API.Data;
using Coiny.API.DTOs.Auth;
using Coiny.API.Models;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using QRCoder;

namespace Coiny.API.Services;

// Stored in the existing central Identity token table. The entire state is encrypted
// with installation Data Protection keys; recovery codes are additionally hashed.
public sealed class AuthenticatorMfaService(IControlContext db, UserManager<ApplicationUser> users,
    AccountSecurityService security, IDataProtectionProvider protection, TimeProvider clock)
{
    public const string Provider = "Coiny.Mfa";
    private const string Name = "Authenticator";
    private readonly IDataProtector stateProtector = protection.CreateProtector("Coiny.UserMfa.State.v1");
    private readonly ITimeLimitedDataProtector setupProtector = protection.CreateProtector("Coiny.UserMfa.Setup.v1").ToTimeLimitedDataProtector();
    private readonly ITimeLimitedDataProtector loginProtector = protection.CreateProtector("Coiny.UserMfa.Login.v1").ToTimeLimitedDataProtector();
    private sealed record State(string Key, string Version, long LastStep, string[] RecoveryHashes);
    private sealed record Setup(string UserId, string Stamp, string Key);
    private sealed record Challenge(string UserId, string Stamp, string Version);
    public sealed record Enrollment(string SetupToken, string SharedKey, string QrCodeDataUrl);
    public sealed record LoginChallenge(bool RequiresMfa, string Challenge, DateTimeOffset ExpiresAt);
    public sealed record EnrollmentResult(AuthResponse Session, string[] RecoveryCodes);
    private DbSet<IdentityUserToken<string>> Tokens => db.Set<IdentityUserToken<string>>();
    private Task<IdentityUserToken<string>?> Row(string id) => Tokens.AsNoTracking().SingleOrDefaultAsync(t => t.UserId == id && t.LoginProvider == Provider && t.Name == Name);
    private State Read(string value) => JsonSerializer.Deserialize<State>(stateProtector.Unprotect(value))!;
    private string Write(State state) => stateProtector.Protect(JsonSerializer.Serialize(state));
    private static string Hash(string code) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(code.Replace("-", "").Replace(" ", "").ToUpperInvariant())));
    private static string[] NewCodes() => Enumerable.Range(0, 10).Select(_ => Convert.ToHexString(RandomNumberGenerator.GetBytes(16))).ToArray();
    private static string[] FormatCodes(string[] codes) => codes.Select(c => string.Join("-", Enumerable.Range(0,4).Select(i=>c.Substring(i*8,8)))).ToArray();
    private static void Require(IdentityResult result) { if (!result.Succeeded) throw new InvalidOperationException("Could not save account security changes."); }

    public async Task<object> Status(ApplicationUser user) {
        var row = await Row(user.Id);
        return new { Enabled = user.TwoFactorEnabled || row != null, RecoveryCodesRemaining = row == null ? 0 : Read(row.Value!).RecoveryHashes.Length };
    }
    public async Task<bool> Enabled(ApplicationUser user) => user.TwoFactorEnabled || await Row(user.Id) != null;
    public async Task<Enrollment?> Begin(ApplicationUser user, string password) {
        if (!await Password(user, password) || await Enabled(user)) return null;
        var key = TotpAuthenticator.NewKey();
        var uri = $"otpauth://totp/{Uri.EscapeDataString("Coiny:" + user.Email)}?secret={key}&issuer=Coiny&algorithm=SHA1&digits=6&period=30";
        using var data = QRCodeGenerator.GenerateQrCode(uri, QRCodeGenerator.ECCLevel.Q);
        using var png = new PngByteQRCode(data);
        return new Enrollment(setupProtector.Protect(JsonSerializer.Serialize(new Setup(user.Id, user.SecurityStamp!, key)), TimeSpan.FromMinutes(10)),
            key, "data:image/png;base64," + Convert.ToBase64String(png.GetGraphic(6)));
    }
    public async Task<EnrollmentResult?> Enable(ApplicationUser user, string token, string code, string device) {
        Setup setup;
        try { setup = JsonSerializer.Deserialize<Setup>(setupProtector.Unprotect(token))!; }
        catch (Exception e) when (e is CryptographicException or JsonException or ArgumentException) { return null; }
        if (setup.UserId != user.Id || setup.Stamp != user.SecurityStamp || await users.IsLockedOutAsync(user)) return null;
        var step = TotpAuthenticator.Verify(setup.Key, code, clock.GetUtcNow());
        if (step == null) { await users.AccessFailedAsync(user); return null; }
        await using var tx = await db.Database.BeginTransactionAsync();
        // Updating the Identity row first serializes enrollment/security changes.
        Require(await users.UpdateSecurityStampAsync(user));
        if (await Enabled(user)) return null;
        var codes = NewCodes();
        Tokens.Add(new() {UserId=user.Id, LoginProvider=Provider, Name=Name, Value=Write(new(setup.Key,Guid.NewGuid().ToString("N"),step.Value,codes.Select(Hash).ToArray()))});
        await db.SaveChangesAsync();
        Require(await users.SetTwoFactorEnabledAsync(user,true));
        Require(await users.ResetAccessFailedCountAsync(user));
        await security.MarkAllRevokedAsync(user.Id);
        var session = await security.StartSessionAsync(user,device);
        await tx.CommitAsync(); return new(session,FormatCodes(codes));
    }
    public async Task<LoginChallenge?> ChallengeFor(ApplicationUser user) {
        var row = await Row(user.Id);
        if (row == null) return null; // Never fall back to password-only login if MFA storage is missing.
        var state = Read(row.Value!);
        return new(true, loginProtector.Protect(JsonSerializer.Serialize(new Challenge(user.Id,user.SecurityStamp!,state.Version)),TimeSpan.FromMinutes(5)),clock.GetUtcNow().AddMinutes(5));
    }
    public async Task<AuthResponse?> VerifyLogin(string token, string code, bool recoveryCode, string device) {
        Challenge challenge;
        try { challenge=JsonSerializer.Deserialize<Challenge>(loginProtector.Unprotect(token))!; }
        catch (Exception e) when (e is CryptographicException or JsonException or ArgumentException) { return null; }
        var user=await users.FindByIdAsync(challenge.UserId);
        if(user == null || user.SecurityStamp != challenge.Stamp || await users.IsLockedOutAsync(user)) return null;
        var row=await Row(user.Id); if(row == null) return null;
        var state=Read(row.Value!); if(state.Version != challenge.Version) return null;
        var next=Consume(state,code,recoveryCode);
        if(next == null) { await users.AccessFailedAsync(user); return null; }
        await using var tx=await db.Database.BeginTransactionAsync();
        if(!await Replace(user.Id,row.Value!,next)) return null;
        // A security-stamp change racing verification must invalidate this challenge.
        db.ChangeTracker.Clear(); user=await users.FindByIdAsync(user.Id);
        if(user == null || user.SecurityStamp != challenge.Stamp || await users.IsLockedOutAsync(user)) return null;
        Require(await users.ResetAccessFailedCountAsync(user));
        var session=await security.StartSessionAsync(user,device);
        await tx.CommitAsync(); return session;
    }
    private State? Consume(State state,string code,bool recovery) {
        if(recovery) {
            var hash=Hash(code); if(!state.RecoveryHashes.Contains(hash)) return null;
            return state with {Version=Guid.NewGuid().ToString("N"),RecoveryHashes=state.RecoveryHashes.Where(h=>h!=hash).ToArray()};
        }
        var step=TotpAuthenticator.Verify(state.Key,code,clock.GetUtcNow());
        return step == null || step <= state.LastStep ? null : state with {Version=Guid.NewGuid().ToString("N"),LastStep=step.Value};
    }
    private async Task<bool> Replace(string id,string previous,State state) => await Tokens.Where(t=>t.UserId==id && t.LoginProvider==Provider && t.Name==Name && t.Value==previous)
        .ExecuteUpdateAsync(s=>s.SetProperty(t=>t.Value,Write(state))) == 1;
    public async Task<bool> Password(ApplicationUser user,string password) {
        if(await users.IsLockedOutAsync(user)) return false;
        if(await users.CheckPasswordAsync(user,password)) return true;
        if(!user.LockoutEnabled) Require(await users.SetLockoutEnabledAsync(user,true));
        await users.AccessFailedAsync(user); return false;
    }
    public async Task<EnrollmentResult?> Manage(ApplicationUser user,string password,string code,bool recovery,bool disable,string device) {
        if(!await Password(user,password)) return null;
        var row=await Row(user.Id); if(row == null) return null;
        var next=Consume(Read(row.Value!),code,recovery);
        if(next == null) { await users.AccessFailedAsync(user); return null; }
        await using var tx=await db.Database.BeginTransactionAsync();
        var codes=disable ? [] : NewCodes();
        if(!await Replace(user.Id,row.Value!,disable ? next : next with {RecoveryHashes=codes.Select(Hash).ToArray()})) return null;
        if(disable) {
            await Tokens.Where(t=>t.UserId==user.Id && t.LoginProvider==Provider && t.Name==Name).ExecuteDeleteAsync();
            Require(await users.SetTwoFactorEnabledAsync(user,false));
        }
        Require(await users.UpdateSecurityStampAsync(user));
        Require(await users.ResetAccessFailedCountAsync(user));
        await security.MarkAllRevokedAsync(user.Id);
        var session=await security.StartSessionAsync(user,device);
        await tx.CommitAsync(); return new(session,FormatCodes(codes));
    }
}
