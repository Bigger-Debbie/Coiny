namespace Coiny.API.Models;

// Review metadata only: never changes transactions, payment links, or budget amounts.
public class RecurringExpenseReview : BaseEntity
{
    public int HouseholdId { get; set; }
    public Household Household { get; set; } = null!;
    public string PatternKey { get; set; } = string.Empty;
    public bool Dismissed { get; set; }
    public decimal ReviewedAmount { get; set; }
    public Guid? AllocationKey { get; set; }
}
