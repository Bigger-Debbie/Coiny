using Coiny.API.DTOs.Households;
using Coiny.API.Interfaces;
using Coiny.API.Models;
using Coiny.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Coiny.API.Controllers;

[ApiController]
[HouseholdExceptionFilter]
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

    [HttpGet("members")]
    public async Task<ActionResult<IReadOnlyList<HouseholdMemberResponse>>> Members() => Ok(await _householdService.GetMembersAsync());

    [HttpDelete("members/{membershipId:int:min(1)}")]
    public async Task<IActionResult> RemoveMember(int membershipId)
    {
        await _householdService.RemoveMemberAsync(membershipId);
        return NoContent();
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
        catch (HouseholdMembershipConflictException ex)
        {
            return Conflict(new ProblemDetails { Status = 409, Detail = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Coiny.API.Data.Hybrid.HouseholdStorageUnavailableException) { throw; }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }
}