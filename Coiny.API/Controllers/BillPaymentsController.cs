using Coiny.API.DTOs.Budgets;
using Coiny.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace Coiny.API.Controllers;
[ApiController, Authorize, BudgetApiExceptionFilter, Route("api/budgets/bills/{key:guid}/{date}")]
public class BillPaymentsController(BillPaymentService payments) : ControllerBase
{
    [HttpPut("payment")]
    public async Task<ActionResult<BillPaymentResponse>> Save(Guid key, DateOnly date, BillPaymentRequest request) => Ok(await payments.SaveAsync(key, date, request));
    [HttpGet("transactions")]
    public async Task<ActionResult<IReadOnlyList<BillTransactionCandidate>>> Candidates(Guid key, DateOnly date, [FromQuery] string? search) => Ok(await payments.CandidatesAsync(key, date, search));
}
