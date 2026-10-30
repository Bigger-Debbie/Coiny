using System.Text.Json.Serialization;
using System.ComponentModel.DataAnnotations;
using Coiny.API.Interfaces;
using Coiny.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace Coiny.API.Controllers;
public class SaveRecurringSetting {
    [JsonRequired] public bool Enabled {get;set;}
    [JsonRequired,Range(0,int.MaxValue)] public int ExpectedEditVersion {get;set;}
}
public class ReadAlertRequest {
    [JsonRequired,Range(1,int.MaxValue)] public int Version {get;set;}
}
[ApiController,Authorize,BudgetApiExceptionFilter,Route("api/alerts")]
[Coiny.API.Data.Hybrid.HouseholdStorage]
public class HouseholdAlertsController(HouseholdAlertsService alerts):ControllerBase {
    [HttpGet] public async Task<ActionResult<AlertsResponse>> List([FromQuery]int page=1)=>Ok(await alerts.ListAsync(page));
    [HttpGet("unread-count")] public async Task<ActionResult<object>> Count()=>Ok(new {unreadCount=await alerts.CountAsync()});
    [HttpPut("{id:int:min(1)}/read")] public async Task<IActionResult> Read(int id,ReadAlertRequest request) {await alerts.ReadAsync(id,request.Version);return NoContent();}
}
[ApiController,Authorize,BudgetApiExceptionFilter,Route("api/recurring-expenses/settings")]
[Coiny.API.Data.Hybrid.HouseholdStorage]
public class RecurringSettingsController(RecurringAutomationService automation,ICurrentUserService user):ControllerBase {
    [HttpGet] public async Task<ActionResult<RecurringSettingResponse>> Get()=>Ok(await automation.GetAsync(await user.GetCurrentHouseholdIdAsync()));
    [HttpPut] public async Task<ActionResult<RecurringSettingResponse>> Save(SaveRecurringSetting request)=>Ok(await automation.SaveAsync(await user.GetCurrentHouseholdIdAsync(),request.Enabled,request.ExpectedEditVersion));
}
