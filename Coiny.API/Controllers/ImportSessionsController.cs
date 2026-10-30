using Coiny.API.DTOs.Transactions;
using Coiny.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace Coiny.API.Controllers;
[ApiController,Authorize,ImportApiExceptionFilter,TransferApiExceptionFilter,Route("api/transaction-imports/sessions")]
[Coiny.API.Data.Hybrid.HouseholdStorage]
public class ImportSessionsController(ImportSessionService sessions) : ControllerBase
{
    [HttpPost("validate")] public async Task<ActionResult<ImportSessionValidation>> Validate(ImportSessionRequest request) => Ok(await sessions.ValidateAsync(request));
    [HttpPost] public async Task<ActionResult<ImportSessionResult>> Import(ImportSessionRequest request) => Ok(await sessions.ImportAsync(request));
}
