using System.ComponentModel.DataAnnotations;
namespace Coiny.API.DTOs.Transactions;
public class ImportUndoSelection
{
    [Required,MinLength(1),MaxLength(8)] public int[] BatchIds { get; set; } = [];
    // Null is a preview with all safe unchanged rows selected. Explicit IDs are the reviewed choice.
    [MaxLength(8000)] public int[]? RemoveTransactionIds { get; set; }
}
public class ImportUndoRequest : ImportUndoSelection
{
    public Guid OperationId { get; set; }
    [Required,MaxLength(64)] public string ReviewedFingerprint { get; set; } = "";
    public bool AcknowledgeChangedTransactions { get; set; }
    public bool AcknowledgePaymentLinks { get; set; }
    public bool AcknowledgeReconciliations { get; set; }
}
public record ImportUndoRow(int Id,int BatchId,int AccountId,string AccountName,DateOnly Date,decimal Amount,string Description,string? Merchant,
    bool ChangedSinceImport,bool OriginalStateUnavailable,int? PairedTransactionId,string? PairedAccountName,int? PairedBatchId,bool HasPaymentLink,bool HasBudgetAssignment);
public record ImportUndoAccount(int AccountId,string Name,decimal Before,decimal After);
public record ImportUndoCheckpoint(int Id,int AccountId,string AccountName,DateOnly StatementDate,decimal StatementBalance);
public record ImportUndoFile(int BatchId,string Filename,int AccountId,string AccountName,int ImportedCount,int RemainingCount,int UndoneCount);
public record ImportUndoHistory(Guid OperationId,DateTime CompletedAt,int RemovedCount,string ActorUserId);
public record ImportUndoPreview(IReadOnlyList<int> BatchIds,IReadOnlyList<ImportUndoFile> RelatedFiles,IReadOnlyList<ImportUndoRow> Rows,
    IReadOnlyList<int> RemoveTransactionIds,IReadOnlyList<ImportUndoAccount> Accounts,IReadOnlyList<ImportUndoCheckpoint> Checkpoints,
    int PaymentLinksToDetach,int BudgetAssignmentsToRemove,int TransferPairsToDetach,int CounterpartsPreserved,string Fingerprint,IReadOnlyList<ImportUndoHistory> UndoHistory);
public record ImportUndoResult(Guid OperationId,DateTime CompletedAt,int RemovedCount,IReadOnlyList<ImportUndoAccount> Accounts,int InvalidatedCheckpoints);
