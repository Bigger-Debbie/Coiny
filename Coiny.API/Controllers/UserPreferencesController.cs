using Coiny.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json.Serialization;
namespace Coiny.API.Controllers;
[ApiController, Authorize, BudgetApiExceptionFilter, Route("api/users/me/preferences")]
public class UserPreferencesController(UserPreferencesService preferences) : ControllerBase
{
    [HttpGet] public async Task<ActionResult<UserPreferences>> Get() => Ok(await preferences.GetAsync());
    [HttpPut, Coiny.API.Data.Hybrid.HouseholdStorage] public async Task<ActionResult<UserPreferences>> Save(UserPreferencesRequest request, [FromServices] RecurringAutomationService automation) => Ok(request.RecurringDetection is { } recurring
        ? await preferences.SaveWithRecurringAsync(request.IncludeMortgageInNetWorth, recurring.Enabled, recurring.ExpectedEditVersion, automation)
        : await preferences.SaveAsync(request.IncludeMortgageInNetWorth));
}
public class UserPreferencesRequest {
    [JsonRequired] public bool IncludeMortgageInNetWorth { get; set; }
    public SaveRecurringSetting? RecurringDetection { get; set; }
}
