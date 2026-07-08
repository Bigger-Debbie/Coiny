using Coiny.API.Enums;

namespace Coiny.API.DTOs.Dashboard;

public class DashboardTransactionResponse
{
    public int Id { get; set; }
    public string AccountName { get; set; } = string.Empty;
    public string? InstitutionName { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public CategoryType CategoryType { get; set; }
    public decimal Amount { get; set; }
    public DateTime TransactionDate { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? Merchant { get; set; } 
}