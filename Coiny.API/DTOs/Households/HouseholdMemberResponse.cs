using Coiny.API.Enums;
namespace Coiny.API.DTOs.Households;

public class HouseholdMemberResponse
{
    public int Id { get; init; }
    public string? FirstName { get; init; }
    public string Email { get; init; } = string.Empty;
    public HouseholdRole Role { get; init; }
    public bool IsCurrentUser { get; init; }
}
