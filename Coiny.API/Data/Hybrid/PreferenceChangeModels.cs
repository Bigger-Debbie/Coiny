namespace Coiny.API.Data.Hybrid;

public sealed class PendingPreferenceChange
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string UserId { get; set; } = "";
    public int HouseholdId { get; set; }
    public bool IncludeMortgage { get; set; }
    public bool ScanEnabled { get; set; }
    public int ExpectedVersion { get; set; }
    public DateTime? CompletedAt { get; set; }
    public bool Cancelled { get; set; }
}
public sealed class PreferenceChangeReceipt
{
    public Guid Id { get; set; }
}
