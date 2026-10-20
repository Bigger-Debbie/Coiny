using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace Coiny.API.Models;

public class ApplicationUser : IdentityUser
{
    public bool IncludeMortgageInNetWorth { get; set; } = true;

    [MaxLength(100)]
    public string? FirstName { get; set; }

    public ICollection<HouseholdMember> HouseholdMemebers { get; set; } = new List<HouseholdMember>();
}
