namespace Coiny.API.DTOs.Dashboard;

public class DashboardSummaryResponse
{
    public NetWorthSummaryResponse NetWorth { get; set; } = new();
    public List<DashboardAccountsResponse> Accounts { get; set; } = [];
    public List<DashboardTransactionResponse> RecentTransactions { get; set; } = [];
}