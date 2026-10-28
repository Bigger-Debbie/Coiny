using Coiny.API.Models;
using Coiny.API.Enums;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
namespace Coiny.API.DTOs.Budgets;
public class BillPaymentRequest
{
    public bool IsSkipped { get; set; }
    [JsonRequired] public bool IsPaid { get; set; }
    public DateOnly? PaidOn { get; set; }
    [MaxLength(500)] public string? Note { get; set; }
    public int? TransactionId { get; set; }
    [MaxLength(50)] public int[]? TransactionIds { get; set; }
}
public record BillPaymentResponse(bool IsPaid, DateOnly? PaidOn, string? Note, int? TransactionId, BillTransactionSummary? LinkedTransaction = null, DateOnly? ActualPostingDate = null)
{
    public bool IsSkipped { get; init; }
    public bool IsFundAllocation { get; init; }
    public string Status => IsSkipped ? "Skipped" : !IsPaid ? "Unpaid" : IsFundAllocation ? "Allocated" : TransactionId.HasValue ? "Linked" : "AwaitingTransaction";
    public IReadOnlyList<BillTransactionSummary> LinkedTransactions { get; init; } = [];
    public IReadOnlyList<int> TransactionIds => LinkedTransactions.Select(t => t.Id).ToArray();
    public static BillPaymentResponse From(BillPayment p)
    {
        var entries = (p.Transaction is null ? Enumerable.Empty<Transaction>() : new[] { p.Transaction })
            .Concat(p.Links.Select(l => l.Transaction)).DistinctBy(t => t.Id)
            .Select(t => new BillTransactionSummary(t.Id, DateOnly.FromDateTime(t.TransactionDate), t.Amount, t.Description,
                DateOnly.FromDateTime(t.TransferDirection == TransferDirection.Out && t.PairedTransaction is not null
                    ? t.PairedTransaction.TransactionDate : t.TransactionDate))).ToArray();
        var first = entries.FirstOrDefault();
        return new(p.IsPaid, p.PaidOn, p.Note, first?.Id ?? p.TransactionId, first, first?.ActualPostingDate) { LinkedTransactions = entries, IsSkipped = p.IsSkipped };
    }
}
public record BillTransactionCandidate(int Id, DateOnly Date, decimal Amount, string Description, string AccountName, string CategoryName, bool ExactAmount,
    CategoryType CategoryType = CategoryType.Expense, TransferDirection? TransferDirection = null);

public record BillTransactionSummary(int Id, DateOnly Date, decimal Amount, string Description, DateOnly? ActualPostingDate = null);
