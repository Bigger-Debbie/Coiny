using Coiny.API.DTOs.Dashboard;

namespace Coiny.API.Interfaces;

public interface IDashboardService
{
    Task<DashboardSummaryResponse> GetSummaryAsync();
}