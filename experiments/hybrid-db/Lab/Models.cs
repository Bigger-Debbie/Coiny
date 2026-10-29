namespace HybridLab;
public record LabOptions(string ControlConnection, string Key, string SigningKey, string DataRoot, string Node, int TargetSchema);
public record Household(Guid Id, string Name, Guid StorageKey, string Status, string Node, int SchemaVersion, Guid? PendingBackup);
public record ImportRow(string Description, long AmountCents);
public record ImportRequest(Guid OperationId, ImportRow[] Rows);
public record ProvisionRequest(Guid UserId, string Name, string? FailAt = null, int SchemaVersion = 2);
public record RestoreRequest(Guid BackupId, bool FailAfterMarker = false);
public class LabFault(int status, string message) : Exception(message) { public int Status => status; }
