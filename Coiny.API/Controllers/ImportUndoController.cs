using Coiny.API.DTOs.Transactions;
using Coiny.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace Coiny.API.Controllers;
[ApiController,Authorize,ImportApiExceptionFilter,Route("api/transaction-imports")]
[Coiny.API.Data.Hybrid.HouseholdStorage]
public class ImportUndoController(ImportUndoService service) : ControllerBase
{
    [HttpGet("{batchId:int:min(1)}/review")] public async Task<ActionResult<ImportUndoPreview>> Review(int batchId)=>Ok(await service.ReviewAsync(batchId));
    [HttpPost("undo/preview")] public async Task<ActionResult<ImportUndoPreview>> Preview(ImportUndoSelection request)=>Ok(await service.PreviewAsync(request));
    [HttpPost("undo")] public async Task<ActionResult<ImportUndoResult>> Undo(ImportUndoRequest request)=>Ok(await service.UndoAsync(request));
}
