using Coiny.API.DTOs.Dashboard;
using Coiny.API.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Coiny.API.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class DashboardController : ControllerBase
{
    private readonly IDashboardService _dashboardService;

    public DashboardController (IDashboardService dashboardService)
    {
        _dashboardService = dashboardService; 
    }

    [HttpGet]
    public async Task<ActionResult<DashboardSummaryResponse>> GetSummary()
    {
        var summary = await _dashboardService.GetSummaryAsync();

        return Ok(summary);
    }
}