namespace Coiny.Api.DTOs.Accounts;

public class CreateAccountRequest
{
    public string Name { get; set; } = string.Empty;
    public string? InstitutionName { get; set; }
    public string Type { get; set; } = string.Empty;
    public decimal OpeningBalance { get; set; }
}
