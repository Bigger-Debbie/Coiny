using System.ComponentModel.DataAnnotations;
using Coiny.API.Enums;
namespace Coiny.API.DTOs.Transactions;
public class ImportSessionRequest
{
    public Guid SubmissionId { get; set; }
    [Required, MinLength(1), MaxLength(8)] public List<TransactionImportRequest> Files { get; set; } = [];
    [Required, MaxLength(4000)] public List<ImportSessionLink> Links { get; set; } = [];
}
public record ImportSessionReference(int AccountId, int? RowId, int? TransactionId, int? ExpectedEditVersion);
public record ImportSessionLink(ImportSessionReference First, ImportSessionReference Second);
public record ImportSessionEntry(ImportSessionReference Reference, string AccountName, DateOnly Date, decimal Amount, TransferDirection Direction, string Description);
public record ImportSessionSuggestion(ImportSessionEntry First, ImportSessionEntry Second);
public record ImportSessionFileCheck(int AccountId, TransactionImportValidation Validation);
public record ImportSessionValidation(bool CanImport, IReadOnlyList<ImportSessionFileCheck> Files, IReadOnlyList<ImportSessionSuggestion> Suggestions, bool MoreSuggestions);
public record ImportSessionResult(Guid SubmissionId, IReadOnlyList<TransactionImportResponse> Files, int LinkedPairs);
