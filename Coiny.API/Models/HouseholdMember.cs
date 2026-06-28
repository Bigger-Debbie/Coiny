using Coiny.API.Enums;

namespace Coiny.API.Models;

public class HouseholdMember
{
    public int Id { get; set; }
    public int HouseholdId { get; set; }
    public Household Household { get; set; } = null;
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser User { get; set; } = null;
    public HouseholdRole Role { get; set; }
}