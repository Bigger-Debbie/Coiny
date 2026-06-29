using Coiny.API.DTOs.Institutions;
using Coiny.API.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Coiny.API.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
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
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }
}