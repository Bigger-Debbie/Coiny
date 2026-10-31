using Coiny.API.DTOs.Auth;
using Coiny.API.Models;
using Coiny.API.Services;
using Coiny.API.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
namespace Coiny.API.Controllers;

[Authorize, ApiController, Route("api/auth/mfa")]
[ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
public sealed class MfaController(UserManager<ApplicationUser> users, AuthenticatorMfaService mfa) : ControllerBase {
    private IActionResult Invalid() => BadRequest(new {message="Your password or code could not be verified, or setup has expired. After several attempts, wait 15 minutes and try again. A code can only be used once; wait for the next code in your app."});
    [HttpGet]
    public async Task<IActionResult> Status() {
        var user=await users.GetUserAsync(User); return user==null ? Unauthorized() : Ok(await mfa.Status(user));
    }
    [HttpPost("setup"),EnableRateLimiting(AccountSecurityRateLimits.Mfa)]
    public async Task<IActionResult> Setup(ConfirmPasswordRequest request) {
        var user=await users.GetUserAsync(User); if(user==null) return Unauthorized();
        var setup=await mfa.Begin(user,request.CurrentPassword); return setup==null ? Invalid() : Ok(setup);
    }
    [HttpPost("enable"),EnableRateLimiting(AccountSecurityRateLimits.Mfa)]
    public async Task<IActionResult> Enable(MfaEnableRequest request) {
        var user=await users.GetUserAsync(User); if(user==null) return Unauthorized();
        var result=await mfa.Enable(user,request.SetupToken,request.Code,Request.Headers.UserAgent.ToString());
        return result==null ? Invalid() : Ok(new {result.Session.Token,result.RecoveryCodes});
    }
    [HttpPost("disable"),EnableRateLimiting(AccountSecurityRateLimits.Mfa)]
    public Task<IActionResult> Disable(MfaManageRequest request) => Manage(request,true);
    [HttpPost("recovery-codes"),EnableRateLimiting(AccountSecurityRateLimits.Mfa)]
    public Task<IActionResult> Codes(MfaManageRequest request) => Manage(request,false);
    private async Task<IActionResult> Manage(MfaManageRequest request,bool disable) {
        var user=await users.GetUserAsync(User); if(user==null) return Unauthorized();
        var result=await mfa.Manage(user,request.CurrentPassword,request.Code,request.RecoveryCode,disable,Request.Headers.UserAgent.ToString());
        return result==null ? Invalid() : Ok(new {result.Session.Token,result.RecoveryCodes});
    }
}
