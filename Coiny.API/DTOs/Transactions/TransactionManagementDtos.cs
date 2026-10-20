using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Coiny.API.Enums;
namespace Coiny.API.DTOs.Transactions;

public class UpdateTransactionRequest
{
    [JsonRequired, Range(0, int.MaxValue)] public int ExpectedEditVersion { get; set; }
    public int? ExpectedPairedEditVersion { get; set; }
    [JsonRequired, Range(1, int.MaxValue)] public int AccountId { get; set; }
    [JsonRequired] public CategoryType? CategoryType { get; set; }
    public int? CategoryId { get; set; }
    [JsonRequired] public decimal Amount { get; set; }
    [JsonRequired] public DateOnly? Date { get; set; }
    [Required, MaxLength(250)] public string Description { get; set; } = string.Empty;
    [MaxLength(150)] public string? Merchant { get; set; }
    [MaxLength(1000)] public string? Notes { get; set; }
    [JsonRequired] public bool IsCleared { get; set; }
    public TransferDirection? Direction { get; set; }
    public TransferCounterparty? Counterparty { get; set; }
}
public record TransactionBillLink(Guid AllocationKey, DateOnly DueDate, bool IsPaid);
public record TransactionEditDetails(TransactionResponse Transaction, int? PairedEditVersion, TransactionBillLink? Bill);
