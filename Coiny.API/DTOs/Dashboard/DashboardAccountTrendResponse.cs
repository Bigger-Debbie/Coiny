using Coiny.API.Enums;

namespace Coiny.API.DTOs.Dashboard;

public class DashboardAccountTrendResponse
{
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public decimal? AbsoluteChange { get; set; }
    public decimal? PercentageChange { get; set; }
    public TrendDirection Direction { get; set; }
}
