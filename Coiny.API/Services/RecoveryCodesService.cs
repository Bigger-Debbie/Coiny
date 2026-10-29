using System.Security.Cryptography;
using System.Text;
using Coiny.API.Data;
using Coiny.API.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
namespace Coiny.API.Services;

public class RecoveryCodesService(CoinyContext db, UserManager<ApplicationUser> users,
    AccountSecurityService security, TimeProvider clock)
{
    public const int BatchSize = 10;
    // Each code has 128 bits of random entropy. SHA-256 hashes also bind it to the user
    // and current security stamp, so password resets/changes/sign-out-everywhere invalidate it.
    private static string Hash(ApplicationUser user, string code) => Convert.ToHexString(SHA256.HashData(
        Encoding.UTF8.GetBytes($"{user.Id}:{user.SecurityStamp}:{code}")));
    private static string Normalize(string code) => new(code.Where(c => c != '-' && !char.IsWhiteSpace(c)).Select(char.ToUpperInvariant).ToArray());

    public async Task<string[]> GenerateAsync(ApplicationUser user)
    {
        await using var transaction = await db.Database.BeginTransactionAsync();
        await db.AuthRecoveryCodes.Where(code => code.UserId == user.Id).ExecuteDeleteAsync();
        var result = Enumerable.Range(0, BatchSize).Select(_ => Convert.ToHexString(RandomNumberGenerator.GetBytes(16))).ToArray();
        db.AuthRecoveryCodes.AddRange(result.Select(code => new AuthRecoveryCode {
            UserId = user.Id, CodeHash = Hash(user, code), CreatedAt = clock.GetUtcNow().UtcDateTime
        }));
        await db.SaveChangesAsync(); await transaction.CommitAsync();
        return result.Select(code => string.Join("-", Enumerable.Range(0, 4).Select(i => code.Substring(i * 8, 8)))).ToArray();
    }
    public Task<int> CountAsync(string userId) => db.AuthRecoveryCodes.CountAsync(code => code.UserId == userId);
    public Task<int> RevokeAsync(string userId) => db.AuthRecoveryCodes.Where(code => code.UserId == userId).ExecuteDeleteAsync();

    public async Task<bool> RecoverAsync(string email, string suppliedCode, string newPassword)
    {
        var normalized = Normalize(suppliedCode);
        if (normalized.Length != 32 || normalized.Any(c => !char.IsAsciiHexDigit(c))) return false;
        // Code consumption, password update, lockout clearing, and session revocation
        // commit together. Failed validation or concurrent redemption cannot consume a code.
        await using var transaction = await db.Database.BeginTransactionAsync();
        var user = await users.FindByEmailAsync(email.Trim());
        if (user is null) return false;
        var hash = Hash(user, normalized);
        var consumed = await db.AuthRecoveryCodes.Where(code => code.UserId == user.Id && code.CodeHash == hash).ExecuteDeleteAsync();
        if (consumed != 1) return false;
        var token = await users.GeneratePasswordResetTokenAsync(user);
        if (!(await users.ResetPasswordAsync(user, token, newPassword)).Succeeded) return false;
        if (!(await users.ResetAccessFailedCountAsync(user)).Succeeded || !(await users.SetLockoutEndDateAsync(user, null)).Succeeded) return false;
        await security.MarkAllRevokedAsync(user.Id);
        await transaction.CommitAsync(); return true;
    }
}
