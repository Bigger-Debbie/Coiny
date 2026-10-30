using System.ComponentModel.DataAnnotations;
using Coiny.API.DTOs.Transactions;
using Coiny.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
namespace Coiny.API.Controllers;
[ApiController, Authorize, TransactionManagementExceptionFilter, Route("api/transactions")]
[Coiny.API.Data.Hybrid.HouseholdStorage]
public class TransactionManagementController(TransactionManagementService transactions) : ControllerBase
{
    [HttpGet("{id:int:min(1)}")]
    public async Task<ActionResult<TransactionEditDetails>> Get(int id) => Ok(await transactions.GetAsync(id));
    [HttpPut("{id:int:min(1)}")]
    public async Task<ActionResult<TransactionEditDetails>> Update(int id, UpdateTransactionRequest request) => Ok(await transactions.UpdateAsync(id, request));
    [HttpDelete("{id:int:min(1)}")]
    public async Task<IActionResult> Delete(int id, [FromQuery, Required, Range(0, int.MaxValue)] int? expectedEditVersion, [FromQuery, Range(0, int.MaxValue)] int? expectedPairedEditVersion)
    { await transactions.DeleteAsync(id, expectedEditVersion!.Value, expectedPairedEditVersion); return NoContent(); }
}
public sealed class TransactionManagementExceptionFilterAttribute : ExceptionFilterAttribute
{
    public override void OnException(ExceptionContext context)
    {
        int? status = context.Exception switch {
            TransactionNotFoundException => 404, TransactionConflictException => 409,
            HouseholdMembershipRequiredException => 403, UnauthorizedAccessException => 401, ArgumentException => 400, _ => null,
        };
        if (status is null) return;
        context.Result = new ObjectResult(new ProblemDetails { Status = status, Detail = context.Exception.Message }) { StatusCode = status };
        context.ExceptionHandled = true;
    }
}
