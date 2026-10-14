using Coiny.API.DTOs.Trends;

namespace Coiny.API.Interfaces;

public interface ITrendService
{
    /// <summary>
    /// Calculates a trend from chronologically ordered points.
    /// Missing dates must be filled by the caller when needed.
    /// </summary>
    TrendResult CalculateTrend(IEnumerable<TrendDataPoint> points);
}
