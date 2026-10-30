using Coiny.API.DTOs.Transactions;
using Coiny.API.Interfaces;
using Coiny.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;

namespace Coiny.API.Controllers;

[ApiController, Authorize, TransferApiExceptionFilter, Route("api/transfers")]
[Coiny.API.Data.Hybrid.HouseholdStorage]
public class TransfersController(ITransferService transfers) : ControllerBase
{
    [HttpPost("candidates")]
    public async Task<ActionResult<List<TransactionResponse>>> CreationCandidates(CreateTransferRequest request) =>
        Ok(await transfers.GetCreationCandidatesAsync(request));
    [HttpGet("entries/{transactionId:int:min(1)}/candidates")]
    public async Task<ActionResult<List<TransactionResponse>>> Candidates(int transactionId, [FromQuery] int accountId, [FromQuery] int days = 7) =>
        Ok(await transfers.GetCandidatesAsync(transactionId, accountId, days));
    [HttpPost("entries/{transactionId:int:min(1)}/counterpart")]
    public async Task<ActionResult<TransferPairResponse>> Counterpart(int transactionId, CreateTransferCounterpartRequest request) =>
        StatusCode(201, await transfers.CreateCounterpartAsync(transactionId, request));
    [HttpPost]
    public async Task<ActionResult<TransferPairResponse>> Create(CreateTransferRequest request) =>
        StatusCode(201, await transfers.CreateAsync(request));
    [HttpPost("entries")]
    public async Task<ActionResult<TransactionResponse>> Entry(CreateTransferEntryRequest request) =>
        StatusCode(201, await transfers.CreateEntryAsync(request));
    [HttpPost("links")]
    public async Task<ActionResult<TransferPairResponse>> Link(LinkTransferRequest request) => Ok(await transfers.LinkAsync(request));
    [HttpDelete("links/{transactionId:int:min(1)}")]
    public async Task<IActionResult> Unlink(int transactionId, [FromQuery] int? expectedEditVersion, [FromQuery] int? expectedPairedEditVersion) { await transfers.UnlinkAsync(transactionId, expectedEditVersion, expectedPairedEditVersion); return NoContent(); }
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
