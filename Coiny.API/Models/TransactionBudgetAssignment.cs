namespace Coiny.API.Models;

public class TransactionBudgetAssignment : BaseEntity
{
    public int HouseholdId { get; set; }
    public Household Household { get; set; } = null!;
    public int TransactionId { get; set; }
    public Transaction Transaction { get; set; } = null!;
    // A null target deliberately leaves this movement unassigned, overriding a category rule.
    public Guid? AllocationKey { get; set; }
}
