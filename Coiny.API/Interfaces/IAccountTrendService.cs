using Coiny.API.DTOs.Trends;
using Coiny.API.Enums;

namespace Coiny.API.Interfaces;

public interface IAccountTrendService
{
    /// <summary>
    /// Builds an inclusive daily trend. Null account IDs select all household
    /// accounts; an empty collection selects none. Duplicate IDs count once.
    /// Future start dates are rejected; future end dates are capped at today
    /// in the application timezone (America/New_York).
    /// </summary>
    Task<TrendResult> GetTrendAsync(
        DateOnly startDate,
        DateOnly endDate,
        IEnumerable<int>? accountIds = null,
        AccountTrendAggregation aggregation = AccountTrendAggregation.SumBalances);
}
