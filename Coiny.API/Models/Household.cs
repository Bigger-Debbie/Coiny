namespace Coiny.API.Models;

public class Household : BaseEntity
{
    public string Name { get; set; }
    public ICollection<HouseholdMember> HouseboldMembers { get; set; } = new List<HouseholdMember>();
}