using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
namespace Coiny.API.DTOs.Accounts;

public class ReconciliationCheckRequest
{
    [Required] public DateOnly? StatementDate { get; set; }
    [JsonRequired] public decimal StatementBalance { get; set; }
}
public class ConfirmReconciliationRequest : ReconciliationCheckRequest
{
    [Required, MinLength(64), MaxLength(64)] public string ReviewedFingerprint { get; set; } = "";
    [JsonRequired] public bool TransactionsReviewed { get; set; }
}
public class AdjustReconciliationRequest : ConfirmReconciliationRequest
{
    [JsonRequired] public Guid OperationId { get; set; }
    [JsonRequired] public bool AdjustmentAcknowledged { get; set; }
    [Required, MinLength(1), MaxLength(500)] public string Reason { get; set; } = "";
}
public record ReconciliationCheck(DateOnly StatementDate, decimal StatementBalance, decimal CalculatedBalance, decimal Difference, bool Matches, string LedgerFingerprint, int TransactionCount);
public record AccountReconciliationResponse(int Id, DateOnly StatementDate, decimal StatementBalance, DateTime ConfirmedAt, string ConfirmedByName, bool NeedsReview, DateTime? InvalidatedAt, string? InvalidationReason, int? AdjustmentTransactionId = null, decimal? AdjustmentAmount = null, string? AdjustmentReason = null);
