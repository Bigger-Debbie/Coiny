namespace Coiny.Api.DTOs.Accounts;

public class AccountResponse
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? InstitutionName { get; set; }
    public string Type { get; set; } = string.Empty;
    public decimal OpeningBalance { get; set; }
    public bool IsActive { get; set; }
}
