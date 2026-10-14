using Coiny.API.DTOs.Dashboard;
using Coiny.API.DTOs.Trends;
using System.ComponentModel.DataAnnotations;
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

    [HttpGet("net-worth-trend")]
    public async Task<ActionResult<TrendResult>> GetNetWorthTrend(
        [FromQuery, Required] DateOnly? startDate,
        [FromQuery, Required] DateOnly? endDate)
    {
        if (!startDate.HasValue || !endDate.HasValue)
            return BadRequest("Start date and end date are required.");

        try
        {
            var trend = await _dashboardService.GetNetWorthTrendAsync(startDate.Value, endDate.Value);
            return Ok(trend);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }
}
