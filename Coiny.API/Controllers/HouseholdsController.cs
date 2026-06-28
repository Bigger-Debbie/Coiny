using Coiny.API.DTOs.Households;
using Coiny.API.Interfaces;
using Coiny.API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Coiny.API.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class HouseholdsController : ControllerBase
{
    private readonly IHouseholdService _householdService;

    public HouseholdsController(IHouseholdService householdService)
    {
        _householdService = householdService;
    }

    [HttpGet("me")]
    public async Task<ActionResult<HouseholdResponse>> GetCurrentHousehold()
    {
        var household = await _householdService.GetCurrentHouseholdAsync();

        if (household is null)
            return NotFound();

        return Ok(household);
    }

    [HttpPost]
    public async Task<ActionResult<HouseholdResponse>> CreateHousehold(CreateHouseholdRequest request)
    {
        try
        {
            var household = await _householdService.CreateHouseholdAsync(request);

            return CreatedAtAction(
                nameof(GetCurrentHousehold),
                household);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }
}