using Coiny.API.Enums;

namespace Coiny.API.Models;

public class Transaction : BaseEntity
{
    public int EditVersion { get; set; }
    public int HouseholdId { get; set; }
    public Household Household { get; set; } = null!;
    public int AccountId { get; set; }
    public Account Account { get; set; } = null!;
    public int CategoryId { get; set; }
    public Category Category { get; set; } = null!;
    public decimal Amount { get; set; }
    public DateTime TransactionDate { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? Merchant { get; set; }
    public string? Notes { get; set; }
    public bool IsCleared { get; set; }
    public TransferDirection? TransferDirection { get; set; }
    public TransferCounterparty? TransferCounterparty { get; set; }
    public int? PairedTransactionId { get; set; }
    public Transaction? PairedTransaction { get; set; }
    public int? ImportBatchId { get; set; }
    public TransactionImportBatch? ImportBatch { get; set; }
    public int? ImportRowId { get; set; }
}
