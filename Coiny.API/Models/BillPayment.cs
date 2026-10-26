namespace Coiny.API.Models;

// An occurrence is identified by household, stable allocation key, and scheduled date.
// Payment status is independent of its optional ledger reference.
public class BillPayment : BaseEntity
{
    public int HouseholdId { get; set; }
    public Household Household { get; set; } = null!;
    public Guid AllocationKey { get; set; }
    public DateOnly DueDate { get; set; }
    public bool IsPaid { get; set; }
    public DateOnly? PaidOn { get; set; }
    public string? Note { get; set; }
    public int? TransactionId { get; set; }
    public Transaction? Transaction { get; set; }
    public ICollection<BillPaymentTransaction> Links { get; set; } = new List<BillPaymentTransaction>();
}
