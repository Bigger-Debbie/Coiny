using Microsoft.AspNetCore.Identity;

namespace Coiny.API.Models;

public class ApplicationUser : IdentityUser
{
    public ICollection<HouseholdMember> HouseholdMemebers { get; set; } = new List<HouseholdMember>();
}