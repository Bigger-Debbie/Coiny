using System.ComponentModel.DataAnnotations;
using Coiny.API.DTOs.MerchantRules;
using Coiny.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace Coiny.API.Controllers;
[ApiController,Authorize,BudgetApiExceptionFilter,Route("api/merchant-rules")]
[Coiny.API.Data.Hybrid.HouseholdStorage]
public class MerchantRulesController(MerchantRulesService service):ControllerBase {
    [HttpGet]public async Task<ActionResult<IReadOnlyList<MerchantRuleResponse>>> List()=>Ok(await service.ListAsync());
    [HttpPost]public async Task<ActionResult<MerchantRuleResponse>> Create(MerchantRuleInput input)=>Ok(await service.SaveAsync(null,input));
    [HttpPut("{id:int:min(1)}")]public async Task<ActionResult<MerchantRuleResponse>> Update(int id,MerchantRuleInput input)=>Ok(await service.SaveAsync(id,input));
    [HttpDelete("{id:int:min(1)}")]public async Task<IActionResult> Delete(int id,[FromQuery,Required,Range(0,int.MaxValue)]int? expectedEditVersion) {await service.DeleteAsync(id,expectedEditVersion!.Value);return NoContent();}
    [HttpPost("preview")]public async Task<ActionResult<RulePreview>> Preview(MerchantRuleInput input)=>Ok(await service.PreviewAsync(input));
}
