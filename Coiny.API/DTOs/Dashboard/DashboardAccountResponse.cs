using Coiny.API.Enums;

namespace Coiny.API.DTOs.Dashboard;

public class DashboardAccountsResponse
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public AccountType AccountType { get; set; }
    public decimal CurrentBalance { get; set; }
    public string? InstitutionName { get; set; }
}