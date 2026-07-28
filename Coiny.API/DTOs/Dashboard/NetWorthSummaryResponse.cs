namespace Coiny.API.DTOs.Dashboard;

public class NetWorthSummaryResponse
{
    public decimal CurrentValue { get; set; }
    public decimal Retirement { get; set; }
    public decimal LiquidFunds { get; set; }
    public decimal Debt { get; set; }
}