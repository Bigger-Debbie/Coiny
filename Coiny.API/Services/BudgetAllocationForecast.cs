using Coiny.API.DTOs.Budgets;
using Coiny.API.Enums;

namespace Coiny.API.Services;

// A remaining-plan estimate, not a prediction from merchant history or a bank balance.
internal static class BudgetAllocationForecast
{
    public static List<BudgetComparisonPoint> Apply(BudgetMonthResponse plan,
        IReadOnlyList<BudgetActualsTransaction> recorded, List<BudgetComparisonPoint> points, DateOnly? through,
        IDictionary<Guid,decimal>? remainingByItem = null)
    {
        if (through is null || points.Count == 0 || through >= points[^1].Date) return points;
        var anchor = points.SingleOrDefault(p => p.Date == through);
        if (anchor?.ActualSpending is null || anchor.ActualContributions is null) return points;
        var future = points.Where(p => p.Date > through).ToList();
        var spending = future.ToDictionary(p => p.Date, _ => 0m);
        var contributions = future.ToDictionary(p => p.Date, _ => 0m);
        foreach (var item in plan.Allocations)
        {
            var target = item.AllocationType is BudgetAllocationType.Savings or BudgetAllocationType.Investments ? contributions : spending;
            var before = target.Values.Sum();
            var events = plan.Events.Where(e => !e.IsIncome && e.SourceKey == item.SourceKey).OrderBy(e => e.Date).ToList();
            // Linked payments settle their occurrence even when the bank posted them in another
            // period. Individual/category assignments reduce the remaining allowance separately.
            var assigned = recorded.Where(t => t.AllocationKey == item.SourceKey && t.PaymentDueDate is null).Sum(t => t.Amount);
            foreach (var occurrence in events)
            {
                if (occurrence.Payment is { IsPaid: true, TransactionId: not null } paid)
                {
                    if (paid.LinkedTransaction is { } linked)
                    {
                        var postingDate = paid.ActualPostingDate ?? linked.Date;
                        if (postingDate > through && target.ContainsKey(postingDate)) target[postingDate] += linked.Amount;
                    }
                    continue;
                }
                var covered = Math.Min(assigned, occurrence.Amount); assigned -= covered;
                var remaining = occurrence.Amount - covered;
                // Overdue and manually paid items still need a posted transaction. Keep them
                // in the estimate rather than pretending that the commitment disappeared.
                var date = occurrence.Payment?.IsPaid == true || occurrence.Date <= through ? future[0].Date : occurrence.Date;
                if (target.ContainsKey(date)) target[date] += remaining;
            }
            var allowance = Math.Max(0, item.PlannedAmount - events.Sum(e => e.Amount));
            if (remainingByItem is not null && item.SourceKey is Guid sourceKey) remainingByItem[sourceKey] = target.Values.Sum() - before;
            if (allowance > 0)
            {
                // Monthly reports do not contain generated period-end events. A linked goal
                // payment still consumes that month's allowance when its due date is in range.
                var consumed = assigned + recorded.Where(t => t.AllocationKey == item.SourceKey &&
                    t.PaymentDueDate >= points[0].Date && t.PaymentDueDate <= points[^1].Date &&
                    !events.Any(e => e.Date == t.PaymentDueDate)).Sum(t => t.Amount);
                var remaining = Math.Max(0, allowance - consumed);
                var weights = future.Select(p => item.DailyAmounts is null ? 1m : item.DailyAmounts.GetValueOrDefault(p.Date)).ToArray();
                var totalWeight = weights.Sum(); if (totalWeight <= 0) continue;
                // Spend the remaining monthly allowance over the remaining days. Cumulative
                // rounding retains its exact cents, including partially elapsed income periods.
                decimal previous = 0, weight = 0;
                for (var i = 0; i < future.Count; i++)
                {
                    weight += weights[i];
                    var cumulative = decimal.Round(remaining * weight / totalWeight, 2, MidpointRounding.AwayFromZero);
                    target[future[i].Date] += cumulative - previous; previous = cumulative;
                }
                if (remainingByItem is not null && item.SourceKey is Guid key) remainingByItem[key] += previous;
            }
        }
        var totalSpending = anchor.ActualSpending.Value; var totalContributions = anchor.ActualContributions.Value;
        return points.Select(point => {
            if (point.Date < through) return point;
            if (point.Date > through) { totalSpending += spending[point.Date]; totalContributions += contributions[point.Date]; }
            return point with { ForecastSpending = totalSpending, ForecastContributions = totalContributions };
        }).ToList();
    }
}
