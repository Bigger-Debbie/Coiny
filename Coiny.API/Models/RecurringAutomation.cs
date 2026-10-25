namespace Coiny.API.Models;
public class HouseholdRecurringSetting : BaseEntity
{
    public int HouseholdId { get; set; }
    public Household Household { get; set; } = null!;
    public bool Enabled { get; set; }
    public DateOnly? LastScanDate { get; set; }
    public int EditVersion { get; set; }
}
public class HouseholdAlert : BaseEntity
{
    public int HouseholdId { get; set; }
    public Household Household { get; set; } = null!;
    public string PatternKey { get; set; } = string.Empty;
    public string Merchant { get; set; } = string.Empty;
    public string Frequency { get; set; } = string.Empty;
    public decimal TypicalAmount { get; set; }
    public DateOnly NextExpected { get; set; }
    public bool Active { get; set; } = true;
    public int Version { get; set; } = 1;
    public DateTime UpdatedAt { get; set; }
}
public class HouseholdAlertRead
{
    public int AlertId { get; set; }
    public HouseholdAlert Alert { get; set; } = null!;
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser User { get; set; } = null!;
    public int Version { get; set; }
}
