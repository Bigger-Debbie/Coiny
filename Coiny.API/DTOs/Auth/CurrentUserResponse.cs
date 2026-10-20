namespace Coiny.API.DTOs.Auth;

public class CurrentUserResponse
{
    public bool IncludeMortgageInNetWorth { get; set; } = true;
    public string Id { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? FirstName { get; set; }
}
