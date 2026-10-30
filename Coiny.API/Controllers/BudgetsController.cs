using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Coiny.API.DTOs.Budgets;
using Coiny.API.Interfaces;
using Coiny.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Coiny.API.Controllers;

[ApiController]
[BudgetApiExceptionFilter]
[Authorize]
[Route("api/budgets")]
[Coiny.API.Data.Hybrid.HouseholdStorage]
public class BudgetsController(IBudgetService budgets) : ControllerBase
{
    [HttpGet("management")]
    public async Task<ActionResult<BudgetManagementResponse>> Management() => Ok(await budgets.GetManagementAsync());

    [HttpPost("items/preview")]
    public async Task<ActionResult<BudgetItemReview>> ReviewItem(BudgetItemChange request) => Ok(await budgets.ChangeItemAsync(request, true));

    [HttpPost("items")]
    public async Task<ActionResult<BudgetItemReview>> ChangeItem(BudgetItemChange request) => Ok(await budgets.ChangeItemAsync(request, false));

    [HttpGet("setup")]
    public async Task<ActionResult<BudgetSetupStatusResponse>> Setup() => Ok(await budgets.GetSetupStatusAsync());

    [HttpGet("draft")]
    public async Task<ActionResult<BudgetRevisionResponse>> Draft()
    {
        var draft = await budgets.GetDraftAsync();
        return draft is null ? NoContent() : Ok(draft);
    }

    [HttpPost("draft")]
    public async Task<ActionResult<BudgetRevisionResponse>> Start(StartBudgetDraftRequest request) =>
        Ok(await budgets.StartDraftAsync(request.EffectiveFromMonth));

    [HttpPut("draft/{draftId:int:min(1)}")]
    public async Task<ActionResult<BudgetRevisionResponse>> Save(int draftId, BudgetDraftRequest request) =>
        Ok(await budgets.SaveDraftAsync(draftId, request));

    [HttpGet("draft/{draftId:int:min(1)}/preview")]
    public async Task<ActionResult<BudgetDraftPreviewResponse>> Preview(int draftId,
        [FromQuery, Required, Range(0, int.MaxValue)] int? expectedEditVersion,
        [FromQuery, Required] DateOnly? month) =>
        Ok(await budgets.PreviewDraftAsync(draftId, expectedEditVersion!.Value, month!.Value));

    [HttpPost("draft/{draftId:int:min(1)}/publish")]
    public async Task<ActionResult<BudgetRevisionResponse>> Publish(int draftId, PublishBudgetDraftRequest request) =>
        Ok(await budgets.PublishDraftAsync(draftId, request.ExpectedEditVersion));

    [HttpGet("income-period")]
    public async Task<ActionResult<BudgetIncomePeriodResponse>> IncomePeriod([FromQuery] DateOnly? asOf, [FromQuery] Guid? sourceKey) =>
        Ok(await budgets.GetIncomePeriodAsync(asOf, sourceKey));

    [HttpGet("month")]
    public async Task<ActionResult<BudgetMonthResponse>> Month([FromQuery, Required] DateOnly? month) =>
        Ok(await budgets.GetMonthAsync(month!.Value));

}

public sealed class BudgetApiExceptionFilterAttribute : ExceptionFilterAttribute
{
    public override void OnException(ExceptionContext context)
    {
        var status = context.Exception switch {
            BudgetDraftNotFoundException => 404,
            BudgetConflictException => 409,
            HouseholdMembershipRequiredException => 403,
            UnauthorizedAccessException => 401,
            ArgumentException => 400,
            _ => (int?)null,
        };
        if (status is null) return;
        context.Result = new ObjectResult(new ProblemDetails { Status = status.Value, Detail = context.Exception.Message }) { StatusCode = status.Value };
        context.ExceptionHandled = true;
    }
}

public class StartBudgetDraftRequest
{
    public DateOnly? EffectiveFromMonth { get; set; }
}

public class PublishBudgetDraftRequest
{
    [JsonRequired, Range(0, int.MaxValue)]
    public int ExpectedEditVersion { get; set; }
}
