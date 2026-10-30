namespace Coiny.API.Data.Hybrid;

// Single owning application process only. This is not a distributed lease.
public sealed class HybridStorageRoot : IDisposable
{
    private readonly FileStream ownership;
    public string DirectoryPath { get; }
    public string Node { get; }
    public HybridStorageRoot(string directory, string node)
    {
        if (string.IsNullOrWhiteSpace(node) || node.Length > 100) throw new ArgumentException("A valid storage node is required.");
        DirectoryPath = Path.GetFullPath(directory);
        Directory.CreateDirectory(DirectoryPath);
        Node = node;
        ownership = new FileStream(Path.Combine(DirectoryPath, ".coiny-owner.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
    }
    public string DatabasePath(Guid key) => Path.Combine(DirectoryPath, key.ToString("N") + ".db");
    public void Dispose() => ownership.Dispose();
}

public sealed class HouseholdStorageUnavailableException() : InvalidOperationException("Household storage is unavailable.");
public sealed class HouseholdStorageAccessDeniedException() : UnauthorizedAccessException("Household access denied.");
