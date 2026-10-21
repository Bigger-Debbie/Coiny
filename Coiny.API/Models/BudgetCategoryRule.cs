namespace Coiny.API.Models;

public class BudgetCategoryRule : BaseEntity
{
    public int HouseholdId { get; set; }
    public Household Household { get; set; } = null!;
    public int CategoryId { get; set; }
    public Category Category { get; set; } = null!;
    public DateOnly EffectiveFromMonth { get; set; }
    public Guid? AllocationKey { get; set; }
    public int EditVersion { get; set; }
}
