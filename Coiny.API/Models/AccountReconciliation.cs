namespace Coiny.API.Models;

public class AccountReconciliation : BaseEntity
{
    public int HouseholdId { get; set; }
    public Household Household { get; set; } = null!;
    public int AccountId { get; set; }
    public Account Account { get; set; } = null!;
    public DateOnly StatementDate { get; set; }
    public decimal StatementBalance { get; set; }
    public string LedgerFingerprint { get; set; } = "";
    public string ConfirmedByUserId { get; set; } = "";
    public string ConfirmedByName { get; set; } = "";
    public Guid? AdjustmentOperationId { get; set; }
    public int? AdjustmentTransactionId { get; set; }
    public decimal? AdjustmentAmount { get; set; }
    public string? AdjustmentReason { get; set; }
    public string? AdjustmentReviewedFingerprint { get; set; }
    public DateTime? InvalidatedAt { get; set; }
    public string? InvalidationReason { get; set; }
}
