namespace Coiny.API.Models;

public class HouseholdBudget : BaseEntity
{
    public int HouseholdId { get; set; }
    public Household Household { get; set; } = null!;
    public ICollection<BudgetRevision> Revisions { get; set; } = new List<BudgetRevision>();
}
