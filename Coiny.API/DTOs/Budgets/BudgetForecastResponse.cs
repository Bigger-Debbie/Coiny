namespace Coiny.API.DTOs.Budgets;

public record BudgetRemainderPoint(DateOnly Date, decimal Planned, decimal? Actual, decimal? Forecast);
public record BudgetForecastPeriod(DateOnly StartDate, DateOnly EndDate, decimal PlannedIncome,
    decimal PlannedAllocations, decimal PlannedRemainder, decimal ForecastIncome,
    decimal ForecastAllocations, decimal ForecastRemainder, decimal CumulativePlanned, decimal CumulativeForecast);
public class BudgetForecastResponse
{
    public DateOnly StartDate { get; init; }
    public DateOnly EndDate { get; init; }
    public DateOnly? ThroughDate { get; init; }
    public int Months { get; init; }
    public decimal ExpectedIncomeAwaitingTransaction { get; init; }
    public decimal PlannedSavingsContributions { get; init; }
    public decimal ForecastSavingsContributions { get; init; }
    public decimal PlannedInvestmentContributions { get; init; }
    public decimal ForecastInvestmentContributions { get; init; }
    public decimal AdditionalSavings { get; init; }
    public decimal FundingShortfall { get; init; }
    public decimal PotentialSavings { get; init; }
    public decimal ShortfallBeyondSavings { get; init; }
    public IReadOnlyList<BudgetRemainderPoint> Points { get; init; } = [];
    public IReadOnlyList<BudgetForecastPeriod> Periods { get; init; } = [];
}
