using System.ComponentModel.DataAnnotations;
using Coiny.API.DTOs.Budgets;
using Coiny.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Coiny.API.Controllers;

[ApiController, Authorize, BudgetApiExceptionFilter, Route("api/budgets/actuals")]
[Coiny.API.Data.Hybrid.HouseholdStorage]
public class BudgetActualsController(BudgetActualsService service) : ControllerBase
{
    [HttpPost("/api/budgets/available-cash")]
    public async Task<ActionResult<AvailableCashResponse>> AvailableCash(AvailableCashRequest request) => Ok(await service.GetAvailableCashAsync(request));
    [HttpGet("/api/budgets/forecast")]
    public async Task<ActionResult<BudgetForecastResponse>> Forecast([FromQuery] DateOnly? asOf = null, [FromQuery] int months = 3) =>
        Ok(await service.GetForecastAsync(asOf,months));
    [HttpGet]
    public async Task<ActionResult<BudgetActualsResponse>> Get([FromQuery, Required] DateOnly? month,
        [FromQuery] string view = "unassigned", [FromQuery] string? type = null, [FromQuery] int page = 1, [FromQuery] Guid? allocationKey = null) =>
        Ok(await service.GetAsync(month!.Value, view, type, page, allocationKey));
    [HttpGet("period")]
    public async Task<ActionResult<BudgetActualsResponse>> Period([FromQuery] DateOnly? asOf = null,
        [FromQuery] string view = "unassigned", [FromQuery] string? type = null, [FromQuery] int page = 1, [FromQuery] Guid? allocationKey = null) =>
        Ok(await service.GetPeriodAsync(asOf,view,type,page,allocationKey));
    [HttpPut("categories/{categoryId:int:min(1)}")]
    public async Task<IActionResult> Category(int categoryId, SaveBudgetCategoryRuleRequest request)
    { await service.SaveCategoryAsync(categoryId, request); return NoContent(); }
    [HttpPut("transactions/{transactionId:int:min(1)}")]
    public async Task<IActionResult> Transaction(int transactionId, SaveTransactionBudgetAssignmentRequest request)
    { await service.SaveAssignmentAsync(transactionId, request); return NoContent(); }
}
