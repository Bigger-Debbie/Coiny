using System.ComponentModel.DataAnnotations;
using Coiny.API.Enums;

namespace Coiny.API.DTOs.Transactions;

public class TransactionImportRequest
{
    [Range(1, int.MaxValue)] public int AccountId { get; set; }
    public Guid SubmissionId { get; set; }
    [Required, MaxLength(255)] public string Filename { get; set; } = "";
    [Required, MinLength(1), MaxLength(2000)] public List<TransactionImportRow> Rows { get; set; } = [];
}

public class TransactionImportRow
{
    [Range(1, int.MaxValue)] public int RowId { get; set; }
    public DateOnly? Date { get; set; }
    public decimal? Amount { get; set; }
    public TransferDirection? Direction { get; set; }
    public CategoryType? CategoryType { get; set; }
    public int? CategoryId { get; set; }
    public TransferCounterparty Counterparty { get; set; } = TransferCounterparty.Unknown;
    [MaxLength(250)] public string? Description { get; set; }
    [MaxLength(150)] public string? Merchant { get; set; }
    [MaxLength(1000)] public string? Notes { get; set; }
    public bool IsCleared { get; set; } = true;
    public bool AllowDuplicate { get; set; }
}

public record ImportRowCheck(int RowId, IReadOnlyList<string> Errors,
    IReadOnlyList<int> DuplicateTransactionIds, IReadOnlyList<int> DuplicateRowIds);
public record TransactionImportValidation(IReadOnlyList<ImportRowCheck> Rows, bool CanImport,
    decimal CurrentBalance, decimal? ProjectedBalance);
public record TransactionImportResponse(int BatchId, Guid SubmissionId, int AccountId,
    string Filename, int ImportedCount, DateTime CompletedAt, decimal BalanceAfter);
