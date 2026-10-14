using Coiny.API.DTOs.Trends;

namespace Coiny.API.Interfaces;

public interface IAccountBalanceSnapshotService
{
    /// <summary>
    /// Stages closing balances for the affected day and later stored snapshots.
    /// The caller must save pending transactions first, then save these changes
    /// within the same database transaction.
    /// </summary>
    Task RecalculateFromAsync(int accountId, DateOnly affectedDate);

    /// <summary>
    /// Returns snapshots in the inclusive range and the last snapshot before it.
    /// Dates without snapshots are filled by the trend layer.
    /// </summary>
    Task<IReadOnlyList<TrendDataPoint>> GetHistoryAsync(int accountId, DateOnly startDate, DateOnly endDate);
}
