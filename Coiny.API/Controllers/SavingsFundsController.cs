using Coiny.API.DTOs.Savings;
using Coiny.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace Coiny.API.Controllers;
[ApiController,Authorize,BudgetApiExceptionFilter,Route("api/savings-funds")]
public class SavingsFundsController(SavingsFundsService service) : ControllerBase
{
    [HttpGet] public async Task<IActionResult> Get([FromQuery]int? accountId=null)=>Ok(await service.GetAsync(accountId));
    [HttpPost("preview")] public async Task<IActionResult> Preview(SaveSavingsFund request)=>Ok(await service.PreviewAsync(request));
    [HttpPost] public async Task<IActionResult> Save(SaveSavingsFund request)=>Ok(await service.SaveAsync(request));
    [HttpGet("{key:guid}/candidates")] public async Task<IActionResult> Candidates(Guid key,[FromQuery]string kind="Contribution")=>Ok(await service.CandidatesAsync(key,kind));
    [HttpPost("{key:guid}/activity/{operation:guid}/correct")] public async Task<IActionResult> Correct(Guid key,Guid operation,[FromQuery]int expectedEditVersion)=>Ok(await service.CorrectActivityAsync(key,operation,expectedEditVersion));
    [HttpPost("{key:guid}/move")] public async Task<IActionResult> Move(Guid key,MoveSavingsAllocation request)=>Ok(await service.MoveAsync(key,request));
    [HttpPost("{key:guid}/archive")] public async Task<IActionResult> Archive(Guid key,ArchiveSavingsFund request){await service.ArchiveAsync(key,request);return NoContent();}
    [HttpPost("{key:guid}/activity")] public async Task<IActionResult> Activity(Guid key,SavingsActivityRequest request)=>Ok(await service.ActivityAsync(key,request));
}
