using Coiny.API.Enums;

namespace Coiny.API.Models;

public class Category : BaseEntity
{
    public int HouseholdId { get; set; }
    public Household Household { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public CategoryType CategoryType { get; set; }
    public bool IsSystem { get; set; }
    public bool IsActive { get; set; }
    public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
}