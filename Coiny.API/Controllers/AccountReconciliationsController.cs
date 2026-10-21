using Coiny.API.DTOs.Accounts;
using Coiny.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
namespace Coiny.API.Controllers;

[ApiController, Authorize, BudgetApiExceptionFilter, ReconciliationApiExceptionFilter, Route("api/accounts/{accountId:int:min(1)}/reconciliations")]
public class AccountReconciliationsController(AccountReconciliationService service) : ControllerBase
{
    [HttpGet] public async Task<ActionResult<IReadOnlyList<AccountReconciliationResponse>>> History(int accountId) => Ok(await service.HistoryAsync(accountId));
    [HttpPost("check")] public async Task<ActionResult<ReconciliationCheck>> Check(int accountId, ReconciliationCheckRequest request) => Ok(await service.CheckAsync(accountId, request));
    [HttpPost] public async Task<ActionResult<AccountReconciliationResponse>> Confirm(int accountId, ConfirmReconciliationRequest request) => Ok(await service.ConfirmAsync(accountId, request));
}
public class ReconciliationApiExceptionFilterAttribute : ExceptionFilterAttribute
{
    public override void OnException(ExceptionContext context)
    {
        int? status = context.Exception switch { ReconciliationNotFoundException => 404, ReconciliationConflictException => 409, DbUpdateConcurrencyException => 409, ArgumentException => 400, _ => null };
        if (status is null) return;
        context.Result = new ObjectResult(new ProblemDetails { Status = status, Detail = context.Exception.Message }) { StatusCode = status }; context.ExceptionHandled = true;
    }
}
