using Coiny.API.Data;
using Coiny.API.Enums;
using Coiny.API.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json.Serialization;
namespace Coiny.API.Controllers;
[ApiController, Authorize, BudgetApiExceptionFilter, Route("api/accounts/{id:int:min(1)}/loan-subtype")]
public class LoanSubtypeController(CoinyContext context, ICurrentUserService user) : ControllerBase
{
    [HttpPut] public async Task<IActionResult> Save(int id, LoanSubtypeRequest request)
    {
        if (!Enum.IsDefined(request.LoanSubtype)) return BadRequest("Choose a valid loan subtype.");
        var household = await user.GetCurrentHouseholdIdAsync();
        var account = await context.Accounts.SingleOrDefaultAsync(a => a.Id == id && a.HouseholdId == household);
        if (account is null) return NotFound();
        if (account.AccountType != AccountType.Loan) return BadRequest("Only loan accounts have a loan subtype.");
        account.LoanSubtype = request.LoanSubtype; await context.SaveChangesAsync(); return NoContent();
    }
}
public class LoanSubtypeRequest { [JsonRequired] public LoanSubtype LoanSubtype { get; set; } }
