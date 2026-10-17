using Coiny.API.DTOs.Households;
using Coiny.API.DTOs.Invitations;
using Coiny.API.Extensions;
using Coiny.API.Interfaces;
using Coiny.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.RateLimiting;

namespace Coiny.API.Controllers;

[ApiController, Authorize, HouseholdExceptionFilter]
[Route("api/households/invitations")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class HouseholdInvitationsController(IHouseholdInvitationService invitations) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<InvitationStatusResponse>> Status() => Ok(await invitations.GetStatusAsync());

    [HttpPost, EnableRateLimiting(HouseholdInvitationRateLimits.GeneratePolicy)]
    public async Task<ActionResult<GeneratedInvitationResponse>> Generate() => Ok(await invitations.GenerateAsync());

    [HttpDelete]
    public async Task<IActionResult> Revoke() { await invitations.RevokeAsync(); return NoContent(); }

    [HttpPost("preview"), EnableRateLimiting(HouseholdInvitationRateLimits.JoinPolicy)]
    public async Task<ActionResult<InvitationPreviewResponse>> Preview(InvitationCodeRequest request) => Ok(await invitations.PreviewAsync(request.Code));

    [HttpPost("redeem"), EnableRateLimiting(HouseholdInvitationRateLimits.JoinPolicy)]
    public async Task<ActionResult<HouseholdResponse>> Redeem(RedeemInvitationRequest request) =>
        Ok(await invitations.RedeemAsync(request.Code, request.HouseholdId!.Value));
}

public sealed class HouseholdExceptionFilterAttribute : ExceptionFilterAttribute
{
    public override void OnException(ExceptionContext context)
    {
        int? status = context.Exception switch {
            InvitationPermissionException => 403,
            HouseholdMemberPermissionException => 403,
            HouseholdMembershipRequiredException => 403,
            HouseholdMemberNotFoundException => 404,
            ProtectedHouseholdMemberException => 409,
            InvalidInvitationException => 404,
            HouseholdMembershipConflictException => 409,
            UnauthorizedAccessException => 401,
            ArgumentException => 400,
            _ => null,
        };
        if (status is null) return;
        context.Result = new ObjectResult(new ProblemDetails { Status = status, Detail = context.Exception.Message }) { StatusCode = status };
        context.ExceptionHandled = true;
    }
}
