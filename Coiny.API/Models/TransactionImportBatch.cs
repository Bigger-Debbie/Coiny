namespace Coiny.API.Models;

public class TransactionImportBatch : BaseEntity
{
    public int HouseholdId { get; set; }
    public Household Household { get; set; } = null!;
    public int AccountId { get; set; }
    public Account Account { get; set; } = null!;
    public Guid SubmissionId { get; set; }
    public string PayloadHash { get; set; } = "";
    public string Filename { get; set; } = "";
    public string CreatedByUserId { get; set; } = "";
    public int ImportedCount { get; set; }
    public decimal BalanceAfter { get; set; }
}
