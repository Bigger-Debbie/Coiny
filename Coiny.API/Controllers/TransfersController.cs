using Coiny.API.DTOs.Transactions;
using Coiny.API.Interfaces;
using Coiny.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;

namespace Coiny.API.Controllers;

[ApiController, Authorize, TransferApiExceptionFilter, Route("api/transfers")]
public class TransfersController(ITransferService transfers) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<TransferPairResponse>> Create(CreateTransferRequest request) =>
        StatusCode(201, await transfers.CreateAsync(request));
    [HttpPost("entries")]
    public async Task<ActionResult<TransactionResponse>> Entry(CreateTransferEntryRequest request) =>
        StatusCode(201, await transfers.CreateEntryAsync(request));
    [HttpPost("links")]
    public async Task<ActionResult<TransferPairResponse>> Link(LinkTransferRequest request) => Ok(await transfers.LinkAsync(request));
    [HttpDelete("links/{transactionId:int:min(1)}")]
    public async Task<IActionResult> Unlink(int transactionId) { await transfers.UnlinkAsync(transactionId); return NoContent(); }
}

public sealed class TransferApiExceptionFilterAttribute : ExceptionFilterAttribute
{
    public override void OnException(ExceptionContext context)
    {
        int? status = context.Exception switch
        {
            TransferNotFoundException => 404, TransferConflictException => 409,
            DbUpdateConcurrencyException => 409,
            HouseholdMembershipRequiredException => 403, UnauthorizedAccessException => 401,
            ArgumentException => 400, _ => null,
        };
        if (status is null) return;
        context.Result = new ObjectResult(new ProblemDetails { Status = status, Detail = context.Exception.Message }) { StatusCode = status };
        context.ExceptionHandled = true;
    }
}
