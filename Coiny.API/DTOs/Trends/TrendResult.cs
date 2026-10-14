using Coiny.API.Enums;

namespace Coiny.API.DTOs.Trends;

public class TrendResult
{
    public IReadOnlyList<TrendDataPoint> Points { get; init; } = Array.Empty<TrendDataPoint>();
    public decimal? StartingValue { get; init; }
    public decimal? EndingValue { get; init; }
    public decimal? AbsoluteChange { get; init; }
    public decimal? PercentageChange { get; init; }
    public TrendDirection Direction { get; init; } = TrendDirection.Neutral;
}
