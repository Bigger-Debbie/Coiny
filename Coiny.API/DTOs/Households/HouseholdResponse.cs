using Coiny.API.Enums;

namespace Coiny.API.DTOs.Households;

public class HouseholdResponse
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public HouseholdRole Role { get; set; }
}