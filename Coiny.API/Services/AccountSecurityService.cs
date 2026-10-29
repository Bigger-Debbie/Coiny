using System.Security.Claims;
using Coiny.API.Configuration;
using Coiny.API.Data;
using Coiny.API.DTOs.Auth;
using Coiny.API.Interfaces;
using Coiny.API.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
namespace Coiny.API.Services;

public class AccountSecurityService(CoinyContext db, UserManager<ApplicationUser> users,
    ITokenService tokens, IOptions<JwtOptions> jwt, TimeProvider clock)
{
    public async Task<AuthResponse> StartSessionAsync(ApplicationUser user, string device)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        // Expired/revoked sessions contain no useful active-session information.
        await db.AuthSessions.Where(s => s.UserId == user.Id && (s.ExpiresAt <= now || s.RevokedAt != null)).ExecuteDeleteAsync();
        var session = new AuthSession { UserId = user.Id, CreatedAt = now,
            ExpiresAt = now.AddMinutes(jwt.Value.ExpirationMinutes), Device = DescribeDevice(device) };
        db.AuthSessions.Add(session); await db.SaveChangesAsync();
        return new AuthResponse { Token = tokens.CreateToken(user, session.Id), Email = user.Email ?? "" };
    }
    private static string DescribeDevice(string agent)
    {
        var browser = agent.Contains("Edg/") ? "Microsoft Edge" : agent.Contains("Firefox/") ? "Firefox" :
            agent.Contains("Chrome/") ? "Chrome" : agent.Contains("Safari/") ? "Safari" : "Browser";
        var platform = agent.Contains("iPhone") ? "iPhone" : agent.Contains("iPad") ? "iPad" : agent.Contains("Android") ? "Android" :
            agent.Contains("Windows") ? "Windows" : agent.Contains("Macintosh") ? "Mac" : agent.Contains("Linux") ? "Linux" : "unknown device";
        return browser + " on " + platform;
    }
    public static string? SessionId(ClaimsPrincipal principal) => principal.FindFirstValue("jti");
    public async Task<bool> IsSessionValidAsync(ClaimsPrincipal principal)
    {
        var id = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        var stamp = principal.FindFirstValue("security_stamp");
        var sid = SessionId(principal); var now = clock.GetUtcNow().UtcDateTime;
        if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(stamp) || string.IsNullOrEmpty(sid)) return false;
        return await db.AuthSessions.AsNoTracking().AnyAsync(s => s.Id == sid && s.UserId == id &&
            s.RevokedAt == null && s.ExpiresAt > now && s.User.SecurityStamp == stamp);
    }
    public async Task<object[]> SessionsAsync(string userId, string? current)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        return (await db.AuthSessions.AsNoTracking().Where(s => s.UserId == userId && s.RevokedAt == null && s.ExpiresAt > now)
            .OrderByDescending(s => s.CreatedAt).ToListAsync())
            .Select(s => (object)new { s.Id, s.CreatedAt, s.ExpiresAt, s.Device, IsCurrent = s.Id == current }).ToArray();
    }
    public async Task RevokeAsync(string userId, string sessionId) => await db.AuthSessions
        .Where(s => s.UserId == userId && s.Id == sessionId && s.RevokedAt == null)
        .ExecuteUpdateAsync(set => set.SetProperty(s => s.RevokedAt, clock.GetUtcNow().UtcDateTime));
    public async Task RevokeAllAsync(ApplicationUser user)
    {
        var result = await users.UpdateSecurityStampAsync(user);
        if (!result.Succeeded) throw new InvalidOperationException("Could not revoke sessions.");
        await MarkAllRevokedAsync(user.Id);
    }
    public async Task MarkAllRevokedAsync(string userId)
    {
        await db.AuthSessions.Where(s => s.UserId == userId && s.RevokedAt == null)
            .ExecuteUpdateAsync(set => set.SetProperty(s => s.RevokedAt, clock.GetUtcNow().UtcDateTime));
        await db.AuthRecoveryCodes.Where(code => code.UserId == userId).ExecuteDeleteAsync();
    }
}
