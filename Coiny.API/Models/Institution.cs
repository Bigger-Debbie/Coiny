using Coiny.API.Enums;

namespace Coiny.API.Models;

public class Institution : BaseEntity
{
    public int HouseholdId { get; set; }
    public Household Household { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public string? Website { get; set; }
    public bool IsActive { get; set; }
    public ICollection<Account> Accounts { get; set; } = new List<Account>();
}