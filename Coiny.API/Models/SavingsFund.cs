namespace Coiny.API.Models;

// Purpose allocations are independent of the bank ledger. SourceKey is also the budget item key.
public class SavingsFund : BaseEntity
{
    public int HouseholdId { get; set; }
    public Guid SourceKey { get; set; } = Guid.NewGuid();
    public int AccountId { get; set; }
    public Account Account { get; set; } = null!;
    public string Name { get; set; } = "";
    public string PlanJson { get; set; } = "";
    public int EditVersion { get; set; }
    public bool IsArchived { get; set; }
    public ICollection<SavingsActivity> Activities { get; set; } = new List<SavingsActivity>();
}
public class SavingsActivity : BaseEntity
{
    public int SavingsFundId { get; set; }
    public SavingsFund SavingsFund { get; set; } = null!;
    public Guid ComponentKey { get; set; }
    public DateOnly Date { get; set; }
    public decimal Amount { get; set; }
    public string Kind { get; set; } = "Contribution";
    public string? Note { get; set; }
    // SetNull retains the purpose-allocation audit when a ledger entry is removed.
    public int? TransactionId { get; set; }
    public Transaction? Transaction { get; set; }
    public DateOnly? DueDate { get; set; }
    public Guid OperationId { get; set; }
}
