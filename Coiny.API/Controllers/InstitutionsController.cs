using Coiny.API.DTOs.Institutions;
using Coiny.API.Interfaces;
using Coiny.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Coiny.API.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
[Coiny.API.Data.Hybrid.HouseholdStorage]
public class InstitutionsController : ControllerBase
{
    private readonly IInstitutionService _institutionService;

    public InstitutionsController(IInstitutionService institutionService)
    {
        _institutionService = institutionService;
    }

    [HttpGet]
    public async Task<ActionResult<List<InstitutionResponse>>> GetInstitutions()
    {
        var institutions = await _institutionService.GetInstitutionsAsync();

        return Ok(institutions);
    }

    [HttpPut("{id:int:min(1)}")]
    public async Task<ActionResult<InstitutionResponse>> UpdateInstitution(int id, UpdateInstitutionRequest request)
    {
        try { return Ok(await _institutionService.UpdateInstitutionAsync(id, request)); }
        catch (ArgumentException ex) { return Problem(statusCode: 400, detail: ex.Message); }
        catch (KeyNotFoundException ex) { return Problem(statusCode: 404, detail: ex.Message); }
        catch (InstitutionConflictException ex) { return Problem(statusCode: 409, detail: ex.Message); }
    }

    [HttpPost]
    public async Task<ActionResult<InstitutionResponse>> CreateInstitution(CreateInstitutionRequest request)
    {
        try
        {
            var institution = await _institutionService.CreateInstitutionAsync(request);

            return CreatedAtAction(
                nameof(GetInstitutions),
                new { id = institution.Id},
                institution);
        }
        catch (ArgumentException ex) { return BadRequest(ex.Message); }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }
}