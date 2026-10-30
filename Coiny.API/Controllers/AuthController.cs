using Coiny.API.DTOs.Auth;
using Coiny.API.Data;
using Coiny.API.Models;
using Coiny.API.Services;
using Coiny.API.Extensions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;

namespace Coiny.API.Controllers;

[ApiController, Route("api/[controller]")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class AuthController(UserManager<ApplicationUser> users, SignInManager<ApplicationUser> signIn,
    AccountSecurityService security, RecoveryCodesService codes, IPasswordRecoveryEmail recovery, ILogger<AuthController> logger) : ControllerBase
{
    private static readonly ApplicationUser DummyUser = new();
    private static readonly PasswordHasher<ApplicationUser> DummyHasher = new();
    private static readonly string DummyHash = DummyHasher.HashPassword(DummyUser, Guid.NewGuid().ToString());
    private static void CheckDummyPassword(string password) => DummyHasher.VerifyHashedPassword(DummyUser, DummyHash, password);

    private async Task<bool> VerifyPasswordAsync(ApplicationUser user, string password)
    {
        // Older imported/seeded identities may predate lockout settings. Protect those accounts too.
        if (!user.LockoutEnabled && !(await users.SetLockoutEnabledAsync(user, true)).Succeeded) return false;
        return (await signIn.CheckPasswordSignInAsync(user, password, lockoutOnFailure: true)).Succeeded;
    }

    [HttpPost("register"), EnableRateLimiting(AccountSecurityRateLimits.Login)]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.FirstName)) return BadRequest(new { message = "Enter your first name." });
        var user = new ApplicationUser { UserName = request.Email.Trim(), Email = request.Email.Trim(), FirstName = request.FirstName.Trim() };
        var result = await users.CreateAsync(user, request.Password);
        if (!result.Succeeded) return BadRequest(result.Errors.Select(e => new { e.Code, Description =
            e.Code.StartsWith("Duplicate") ? "An account could not be created with those details. Try signing in or recovering your password." : e.Description }));
        return Ok(await security.StartSessionAsync(user, Request.Headers.UserAgent.ToString()));
    }
    [Authorize, HttpGet("me")]
    public async Task<ActionResult<CurrentUserResponse>> GetCurrentUser()
    {
        var user = await users.GetUserAsync(User);
        if (user is null) return Unauthorized();
        return Ok(new CurrentUserResponse { Id = user.Id, Email = user.Email ?? "", FirstName = user.FirstName,
            IncludeMortgageInNetWorth = user.IncludeMortgageInNetWorth });
    }
    [HttpPost("login"), EnableRateLimiting(AccountSecurityRateLimits.Login)]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request)
    {
        var user = await users.FindByEmailAsync(request.Email.Trim());
        if (user is null) { CheckDummyPassword(request.Password); return Unauthorized(new { message = "Invalid email or password. If you've tried several times, wait 15 minutes before trying again." }); }
        if (!await VerifyPasswordAsync(user, request.Password)) return Unauthorized(new { message = "Invalid email or password. If you've tried several times, wait 15 minutes before trying again." });
        return Ok(await security.StartSessionAsync(user, Request.Headers.UserAgent.ToString()));
    }
    [HttpGet("recovery-options")]
    public IActionResult RecoveryOptions() => Ok(new { Email = recovery.IsConfigured, RecoveryCode = true });

    [Authorize, HttpGet("recovery-codes")]
    public async Task<IActionResult> RecoveryCodeStatus() => Ok(new { Remaining = await codes.CountAsync(users.GetUserId(User)!) });

    [Authorize, HttpPost("recovery-codes"), EnableRateLimiting(AccountSecurityRateLimits.Login)]
    public async Task<IActionResult> GenerateRecoveryCodes(ConfirmPasswordRequest request)
    {
        var user = await users.GetUserAsync(User); if (user is null) return Unauthorized();
        if (!await VerifyPasswordAsync(user, request.CurrentPassword))
            return BadRequest(new { message = "Your password could not be verified. After several attempts, wait 15 minutes and try again." });
        return Ok(new { Codes = await codes.GenerateAsync(user) });
    }

    [Authorize, HttpPost("recovery-codes/revoke"), EnableRateLimiting(AccountSecurityRateLimits.Login)]
    public async Task<IActionResult> RevokeRecoveryCodes(ConfirmPasswordRequest request)
    {
        var user = await users.GetUserAsync(User); if (user is null) return Unauthorized();
        if (!await VerifyPasswordAsync(user, request.CurrentPassword))
            return BadRequest(new { message = "Your password could not be verified. After several attempts, wait 15 minutes and try again." });
        await codes.RevokeAsync(user.Id); return NoContent();
    }

    [HttpPost("recover-with-code"), EnableRateLimiting(AccountSecurityRateLimits.CodeRecovery)]
    public async Task<IActionResult> RecoverWithCode(RecoverWithCodeRequest request)
    {
        if (!await codes.RecoverAsync(request.Email, request.RecoveryCode, request.NewPassword))
            return BadRequest(new { message = "The email or recovery code is incorrect, the code is no longer valid, or the new password doesn't meet the requirements." });
        return Ok(new { message = "Your password has been reset and all sessions and recovery codes have been invalidated. Sign in with your new password, then generate a new set of recovery codes in Security." });
    }

    [HttpPost("forgot-password"), EnableRateLimiting(AccountSecurityRateLimits.Recovery)]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordRequest request, CancellationToken cancellationToken)
    {
        if (!recovery.IsConfigured) return Problem(statusCode: 503, title: "Password recovery is unavailable", detail: "Password recovery email hasn't been configured. Contact your Coiny administrator.");
        var user = await users.FindByEmailAsync(request.Email.Trim());
        if (user is not null) {
            var token = await users.GeneratePasswordResetTokenAsync(user);
            try { await recovery.SendAsync(user.Email!, token, cancellationToken); }
            catch (Exception exception) when (exception is System.Net.Mail.SmtpException or OperationCanceledException or InvalidOperationException) {
                // Don't expose account existence or log tokens, email addresses, or SMTP credentials.
                logger.LogWarning("Password recovery email delivery failed ({FailureType}). Check the mail service configuration.", exception.GetType().Name);
            }
        }
        return Ok(new { message = "If an account matches that email, a password reset link will be sent. Check your inbox and spam folder." });
    }
    [HttpPost("reset-password"), EnableRateLimiting(AccountSecurityRateLimits.Login)]
    public async Task<IActionResult> ResetPassword(ResetPasswordRequest request)
    {
        var user = await users.FindByEmailAsync(request.Email.Trim());
        if (user is null) return BadRequest(new { message = "This reset link is invalid or expired. Request a new one." });
        var result = await users.ResetPasswordAsync(user, request.Token, request.NewPassword);
        if (!result.Succeeded) return BadRequest(new { message = "The reset link is invalid or expired, or the new password doesn't meet the password requirements." });
        await users.ResetAccessFailedCountAsync(user); await users.SetLockoutEndDateAsync(user, null);
        await security.MarkAllRevokedAsync(user.Id);
        return Ok(new { message = "Your password has been reset. Sign in with your new password." });
    }
    [Authorize, HttpPost("change-password"), EnableRateLimiting(AccountSecurityRateLimits.Login)]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request, [FromServices] IControlContext db)
    {
        var user = await users.GetUserAsync(User); if (user is null) return Unauthorized();
        if (!await VerifyPasswordAsync(user, request.CurrentPassword))
            return BadRequest(new { message = "Your current password could not be verified. After several attempts, wait 15 minutes and try again." });
        if (request.CurrentPassword == request.NewPassword) return BadRequest(new { message = "Choose a different password." });
        await using var transaction = await db.Database.BeginTransactionAsync();
        var result = await users.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
        if (!result.Succeeded) return BadRequest(new { message = string.Join(" ", result.Errors.Select(e => e.Description)) });
        await security.MarkAllRevokedAsync(user.Id);
        var session = await security.StartSessionAsync(user, Request.Headers.UserAgent.ToString());
        await transaction.CommitAsync();
        return Ok(session);
    }
    [Authorize, HttpGet("sessions")]
    public async Task<IActionResult> Sessions() => Ok(await security.SessionsAsync(users.GetUserId(User)!, AccountSecurityService.SessionId(User)));
    [Authorize, HttpDelete("sessions/{id}")]
    public async Task<IActionResult> RevokeSession(string id) { await security.RevokeAsync(users.GetUserId(User)!, id); return NoContent(); }
    [Authorize, HttpPost("logout")]
    public async Task<IActionResult> Logout() { await security.RevokeAsync(users.GetUserId(User)!, AccountSecurityService.SessionId(User)!); return NoContent(); }
    [Authorize, HttpPost("logout-all"), EnableRateLimiting(AccountSecurityRateLimits.Login)]
    public async Task<IActionResult> LogoutAll(ConfirmPasswordRequest request)
    {
        var user = await users.GetUserAsync(User); if (user is null) return Unauthorized();
        if (!await VerifyPasswordAsync(user, request.CurrentPassword))
            return BadRequest(new { message = "Your password could not be verified. After several attempts, wait 15 minutes and try again." });
        await security.RevokeAllAsync(user); return NoContent();
    }
}
