using Coiny.API.DTOs.Trends;
using Coiny.API.Enums;
using Coiny.API.Interfaces;

namespace Coiny.API.Services;

public class TrendService : ITrendService
{
    public TrendResult CalculateTrend(IEnumerable<TrendDataPoint> points)
    {
        ArgumentNullException.ThrowIfNull(points);

        var dataPoints = points.ToList().AsReadOnly();

        for (var index = 1; index < dataPoints.Count; index++)
        {
            if (dataPoints[index].Date < dataPoints[index - 1].Date)
                throw new ArgumentException("Trend points must be in chronological order.", nameof(points));
        }

        if (dataPoints.Count == 0)
            return new TrendResult { Points = dataPoints };

        var startingValue = dataPoints[0].Value;
        var endingValue = dataPoints[^1].Value;
        var absoluteChange = endingValue - startingValue;

        // Using the starting magnitude preserves the direction of change
        // when a negative balance improves. A zero baseline has no percentage.
        decimal? percentageChange = startingValue == 0
            ? null
            : absoluteChange / Math.Abs(startingValue) * 100m;

        return new TrendResult
        {
            Points = dataPoints,
            StartingValue = startingValue,
            EndingValue = endingValue,
            AbsoluteChange = absoluteChange,
            PercentageChange = percentageChange,
            Direction = absoluteChange switch
            {
                > 0 => TrendDirection.Up,
                < 0 => TrendDirection.Down,
                _ => TrendDirection.Neutral
            }
        };
    }
}
