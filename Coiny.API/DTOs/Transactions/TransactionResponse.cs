using Coiny.API.Enums;

namespace Coiny.API.DTOs.Transactions;

public class TransactionResponse
{
    public int EditVersion { get; set; }
    public string? Notes { get; set; }
    public int? ImportBatchId { get; set; }
    public int Id { get; set; }
    public int AccountId { get; set; }
    public int CategoryId { get; set; }
    public string? InstitutionName { get; set; }
    public string AccountName { get; set; } = string.Empty;
    public AccountType AccountType { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public CategoryType CategoryType { get; set; }
    public decimal Amount { get; set; }
    public DateTime TransactionDate { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? Merchant { get; set; }
    public bool IsCleared { get; set; }
    public TransferDirection? TransferDirection { get; set; }
    public TransferCounterparty? TransferCounterparty { get; set; }
    public int? PairedTransactionId { get; set; }
    public int? CounterpartyAccountId { get; set; }
    public string? CounterpartyAccountName { get; set; }
    public AccountType? CounterpartyAccountType { get; set; }
}
