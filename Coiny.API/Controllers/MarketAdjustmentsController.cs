using Coiny.API.DTOs.Transactions;
using Coiny.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace Coiny.API.Controllers;

[ApiController, Authorize, TransactionManagementExceptionFilter, Route("api/transactions/market-adjustments")]
[Coiny.API.Data.Hybrid.HouseholdStorage]
public class MarketAdjustmentsController(MarketAdjustmentService adjustments) : ControllerBase
{
    [HttpGet("balance")]
    public async Task<ActionResult<MarketAdjustmentBalance>> Balance([FromQuery] int accountId, [FromQuery] DateOnly date, [FromQuery] int? excludingId) => Ok(await adjustments.GetBalanceAsync(accountId, date, excludingId));
    [HttpPost]
    public async Task<ActionResult<TransactionResponse>> Create(MarketAdjustmentRequest request) => Ok(await adjustments.CreateAsync(request));
}
