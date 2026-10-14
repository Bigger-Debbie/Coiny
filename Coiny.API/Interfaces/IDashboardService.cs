using Coiny.API.DTOs.Dashboard;
using Coiny.API.DTOs.Trends;

namespace Coiny.API.Interfaces;

public interface IDashboardService
{
    Task<DashboardSummaryResponse> GetSummaryAsync();
    Task<TrendResult> GetNetWorthTrendAsync(DateOnly startDate, DateOnly endDate);
}
