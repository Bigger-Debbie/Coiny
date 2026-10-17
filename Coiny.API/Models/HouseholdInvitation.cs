namespace Coiny.API.Models;

public class HouseholdInvitation : BaseEntity
{
    public int HouseholdId { get; set; }
    public Household Household { get; set; } = null!;
    public string CreatedByUserId { get; set; } = string.Empty;
    public string CodeHash { get; set; } = string.Empty;
    public long ExpiresAtUtcTicks { get; set; }
    public long? ConsumedAtUtcTicks { get; set; }
    public long? RevokedAtUtcTicks { get; set; }
}
