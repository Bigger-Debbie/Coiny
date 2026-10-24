using Coiny.API.DTOs.Budgets;
using Coiny.API.Enums;
using Microsoft.EntityFrameworkCore;

namespace Coiny.API.Services;

public partial class BudgetActualsService
{
    public async Task<BudgetForecastResponse> GetForecastAsync(DateOnly? asOf = null, int months = 3)
    {
        var periods = await budgets.GetIncomePeriodsAsync(asOf,months);
        var start = periods[0].StartDate!.Value; var end = periods[^1].EndDate!.Value;
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.GetUtcNow(), TimeZoneInfo.FindSystemTimeZoneById("America/New_York")).DateTime);
        var household = await user.GetCurrentHouseholdIdAsync();
        var from = start.ToDateTime(TimeOnly.MinValue); var to = (today < end ? today : end).ToDateTime(TimeOnly.MaxValue);
        // Income is real posted income only. Transfers between household accounts never count
        // as new household income, regardless of which accounts were used for the paycheck.
        var income = await context.Transactions.AsNoTracking().Where(t => t.HouseholdId == household &&
            t.Category.CategoryType == CategoryType.Income && t.TransactionDate >= from && t.TransactionDate <= to)
            .Select(t => new { t.TransactionDate, t.Amount }).ToListAsync();
        var incomeByDate = income.GroupBy(t => DateOnly.FromDateTime(t.TransactionDate)).ToDictionary(g => g.Key,g => g.Sum(t => t.Amount));
        var points = new List<BudgetRemainderPoint>(); var summaries = new List<BudgetForecastPeriod>();
        decimal plannedCarry = 0, actualCarry = 0, forecastCarry = 0, pendingIncome = 0;
        decimal plannedSavings = 0, forecastSavings = 0, plannedInvestments = 0, forecastInvestments = 0;
        if (start > DateOnly.MinValue)
            points.Add(new(start.AddDays(-1),0,start <= today ? 0 : null,start > today ? 0 : null));
        foreach (var period in periods)
        {
            var plan = period.Plan!; var periodStart = period.StartDate!.Value; var periodEnd = period.EndDate!.Value;
            var report = periodStart <= today ? await Report(plan.Month,"all",null,1,null,period) : null;
            IReadOnlyList<BudgetComparisonPoint> projected = period.PlannedPoints;
            var remainingByItem = new Dictionary<Guid,decimal>();
            if (report is null)
            {
                var anchor = periodStart.AddDays(-1);
                var raw = new[] { new BudgetComparisonPoint(anchor,0,0,0,0) }.Concat(period.PlannedPoints).ToList();
                projected = BudgetAllocationForecast.Apply(plan,[],raw,anchor,remainingByItem).Where(p => p.Date >= periodStart).ToList();
            }
            plannedSavings += plan.Allocations.Where(a => a.AllocationType == BudgetAllocationType.Savings).Sum(a => a.PlannedAmount);
            plannedInvestments += plan.Allocations.Where(a => a.AllocationType == BudgetAllocationType.Investments).Sum(a => a.PlannedAmount);
            forecastSavings += report?.ForecastSavingsContributions ?? plan.Allocations.Where(a => a.AllocationType == BudgetAllocationType.Savings).Sum(a => remainingByItem.GetValueOrDefault(a.SourceKey!.Value));
            forecastInvestments += report?.ForecastInvestmentContributions ?? plan.Allocations.Where(a => a.AllocationType == BudgetAllocationType.Investments).Sum(a => remainingByItem.GetValueOrDefault(a.SourceKey!.Value));
            var daily = (report?.Points ?? projected).ToDictionary(p => p.Date);
            var futureIncome = new Dictionary<DateOnly,decimal>();
            var incomeToDate = incomeByDate.Where(p => p.Key >= periodStart && p.Key <= periodEnd).Sum(p => p.Value);
            var credit = incomeToDate;
            if (periodEnd > today)
            {
                foreach (var payday in plan.Events.Where(e => e.IsIncome).OrderBy(e => e.Date))
                {
                    var covered = Math.Min(credit,payday.Amount); credit -= covered;
                    var remaining = payday.Amount - covered;
                    if (remaining == 0) continue;
                    var date = payday.Date > today ? payday.Date : today.AddDays(1);
                    futureIncome[date] = futureIncome.GetValueOrDefault(date) + remaining;
                    if (payday.Date <= today) pendingIncome += remaining;
                }
            }
            decimal plannedIncome = 0, recordedIncome = 0, estimatedIncome = 0;
            decimal periodActual = 0, periodForecast = 0, forecastOut = 0;
            for (var date = periodStart; date <= periodEnd;)
            {
                var p = daily[date];
                plannedIncome += plan.Events.Where(e => e.IsIncome && e.Date == date).Sum(e => e.Amount);
                recordedIncome += incomeByDate.GetValueOrDefault(date);
                estimatedIncome += incomeByDate.GetValueOrDefault(date) + futureIncome.GetValueOrDefault(date);
                var planned = plannedCarry + plannedIncome - p.PlannedSpending - p.PlannedContributions;
                decimal? actual = null, forecast = null;
                if (date <= today)
                {
                    periodActual = recordedIncome - (p.ActualSpending ?? 0) - (p.ActualContributions ?? 0);
                    actual = actualCarry + periodActual;
                    if (date == today) forecast = actual;
                    forecastOut = (p.ActualSpending ?? 0) + (p.ActualContributions ?? 0);
                }
                else
                {
                    forecastOut = (p.ForecastSpending ?? p.ActualSpending ?? 0) + (p.ForecastContributions ?? p.ActualContributions ?? 0);
                    forecast = forecastCarry + estimatedIncome - forecastOut;
                }
                periodForecast = estimatedIncome - forecastOut;
                points.Add(new(date,planned,actual,forecast));
                if (date == periodEnd) break;
                date = date.AddDays(1);
            }
            plannedCarry += plan.ExpectedIncome - plan.Allocated;
            actualCarry += periodActual;
            forecastCarry += periodForecast;
            summaries.Add(new(periodStart,periodEnd,plan.ExpectedIncome,plan.Allocated,plan.ExpectedIncome-plan.Allocated,
                estimatedIncome,forecastOut,periodForecast,plannedCarry,forecastCarry));
        }
        return new() { StartDate=start,EndDate=end,ThroughDate=today < start ? null : today < end ? today : end,Months=months,
            ExpectedIncomeAwaitingTransaction=pendingIncome,Points=points,Periods=summaries,
            PlannedSavingsContributions=plannedSavings,ForecastSavingsContributions=forecastSavings,
            PlannedInvestmentContributions=plannedInvestments,ForecastInvestmentContributions=forecastInvestments,
            AdditionalSavings=Math.Max(0,forecastCarry),FundingShortfall=Math.Max(0,-forecastCarry),
            PotentialSavings=Math.Max(0,forecastSavings+forecastCarry),ShortfallBeyondSavings=Math.Max(0,-forecastSavings-forecastCarry) };
    }
}
