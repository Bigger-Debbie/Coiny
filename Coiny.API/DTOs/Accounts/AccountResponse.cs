using Coiny.API.Enums;

namespace Coiny.API.DTOs.Accounts;

public class AccountResponse
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public AccountType AccountType { get; set; }
    public decimal OpeningBalance { get; set; }
    public decimal CurrentBalance { get; set; }
    public bool IsActive { get; set; }
    public int? InstitutionId { get; set; }
    public string? InstitutionName { get; set; }
}