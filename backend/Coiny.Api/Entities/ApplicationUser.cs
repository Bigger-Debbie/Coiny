using Microsoft.AspNetCore.Identity;

namespace Coiny.Api.Entities;

public class ApplicationUser : IdentityUser
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public List<Account> Accounts { get; set; } = new();
}
