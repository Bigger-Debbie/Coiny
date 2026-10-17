using Coiny.API.DTOs.Transactions;
using Coiny.API.Interfaces;
using Coiny.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Coiny.API.Controllers;

[ApiController, Authorize, ImportApiExceptionFilter, Route("api/transaction-imports")]
public class TransactionImportsController(ITransactionImportService imports) : ControllerBase
{
    [HttpPost("validate")]
    public async Task<ActionResult<TransactionImportValidation>> Validate(TransactionImportRequest request) => Ok(await imports.ValidateAsync(request));
    [HttpPost]
    public async Task<ActionResult<TransactionImportResponse>> Import(TransactionImportRequest request) => Ok(await imports.ImportAsync(request));
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<TransactionImportResponse>>> History([FromQuery] int? accountId) => Ok(await imports.HistoryAsync(accountId));
}

public sealed class ImportApiExceptionFilterAttribute : ExceptionFilterAttribute
{
    public override void OnException(ExceptionContext context)
    {
        if (context.Exception is ImportValidationException validation)
        {
            context.Result = new UnprocessableEntityObjectResult(validation.Validation); context.ExceptionHandled = true; return;
        }
        int? status = context.Exception switch
        {
            ImportConflictException => 409, HouseholdMembershipRequiredException => 403,
            UnauthorizedAccessException => 401, ArgumentException => 400, _ => null,
        };
        if (status is null) return;
        context.Result = new ObjectResult(new ProblemDetails { Status = status, Detail = context.Exception.Message }) { StatusCode = status };
        context.ExceptionHandled = true;
    }
}
