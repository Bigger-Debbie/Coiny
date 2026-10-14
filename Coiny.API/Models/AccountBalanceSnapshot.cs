namespace Coiny.API.Models;

/// <summary>
/// Stores an account's closing balance for a calendar day.
/// The day's record can be updated as transactions change that closing balance.
/// </summary>
public class AccountBalanceSnapshot : BaseEntity
{
    public int AccountId { get; set; }
    public Account Account { get; set; } = null!;
    public DateOnly SnapshotDate { get; set; }
    public decimal Balance { get; set; }
}
