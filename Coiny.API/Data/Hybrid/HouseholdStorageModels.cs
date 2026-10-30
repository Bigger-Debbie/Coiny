namespace Coiny.API.Data.Hybrid;

public enum HouseholdDatabaseState { Provisioning, Ready, Maintenance }

public sealed class HouseholdDatabase
{
    public int HouseholdId { get; set; }
    public Guid StorageKey { get; set; } = Guid.NewGuid();
    public string OwnerNode { get; set; } = "";
    public HouseholdDatabaseState State { get; set; }
    public int SchemaVersion { get; set; }
}

public sealed class HouseholdDatabaseMetadata
{
    public int Id { get; set; } = 1;
    public int HouseholdId { get; set; }
    public Guid StorageKey { get; set; }
    public int SchemaVersion { get; set; }
}
