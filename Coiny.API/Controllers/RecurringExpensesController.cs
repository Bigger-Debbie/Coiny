using Coiny.API.DTOs.Recurring;
using Coiny.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace Coiny.API.Controllers;
[ApiController, Authorize, BudgetApiExceptionFilter, Route("api/recurring-expenses")]
public class RecurringExpensesController(RecurringExpenseService service) : ControllerBase
{
    [HttpPost("scan")]
    public async Task<ActionResult<RecurringScanResponse>> Scan() => Ok(await service.ScanAsync());
    [HttpPut("review")]
    public async Task<IActionResult> Review(ReviewRecurringRequest request) { await service.ReviewAsync(request); return NoContent(); }
}
