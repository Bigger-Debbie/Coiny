using Coiny.API.Models;
using Coiny.API.Enums;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
namespace Coiny.API.DTOs.Budgets;
public class BillPaymentRequest
{
    [JsonRequired] public bool IsPaid { get; set; }
    public DateOnly? PaidOn { get; set; }
    [MaxLength(500)] public string? Note { get; set; }
    public int? TransactionId { get; set; }
}
public record BillPaymentResponse(bool IsPaid, DateOnly? PaidOn, string? Note, int? TransactionId, BillTransactionSummary? LinkedTransaction = null, DateOnly? ActualPostingDate = null)
{
    public string Status => !IsPaid ? "Unpaid" : TransactionId.HasValue ? "Linked" : "AwaitingTransaction";
    public static BillPaymentResponse From(BillPayment p) => new(p.IsPaid, p.PaidOn, p.Note, p.TransactionId,
        p.Transaction is null ? null : new(p.Transaction.Id, DateOnly.FromDateTime(p.Transaction.TransactionDate), p.Transaction.Amount, p.Transaction.Description),
        p.Transaction is null ? null : DateOnly.FromDateTime(p.Transaction.TransferDirection == TransferDirection.Out && p.Transaction.PairedTransaction is not null
            ? p.Transaction.PairedTransaction.TransactionDate : p.Transaction.TransactionDate));
}
public record BillTransactionCandidate(int Id, DateOnly Date, decimal Amount, string Description, string AccountName, string CategoryName, bool ExactAmount,
    CategoryType CategoryType = CategoryType.Expense, TransferDirection? TransferDirection = null);

public record BillTransactionSummary(int Id, DateOnly Date, decimal Amount, string Description);
