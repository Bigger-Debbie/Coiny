namespace Coiny.API.DTOs.Dashboard;

public class DashboardSummaryResponse
{
    public decimal NetWorth { get; set; }
    public List<DashboardAccountsResponse> Accounts { get; set; } = [];
    public List<DashboardTransactionResponse> RecentTransactions { get; set; } = [];
}