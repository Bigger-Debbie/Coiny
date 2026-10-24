using Coiny.API.DTOs.Budgets;

namespace Coiny.API.Services;

public partial class BudgetService
{
    public async Task<IReadOnlyList<BudgetIncomePeriodResponse>> GetIncomePeriodsAsync(DateOnly? asOf, int months)
    {
        if (months is not (0 or 1 or 3 or 6 or 12)) throw new ArgumentException("Choose this period, 1, 3, 6, or 12 months.");
        var published = await Published(await currentUser.GetCurrentHouseholdIdAsync());
        var calendars = new Dictionary<DateOnly,BudgetMonthResponse>();
        var first = await GetPeriod(asOf,null,published,calendars);
        if (first.Plan is null || first.StartDate is null || first.EndDate is null)
            throw new ArgumentException("A complete published income period is needed to forecast. Check your income schedule or choose another period.");
        DateOnly target;
        try { target = months == 0 ? first.EndDate.Value : first.StartDate.Value.AddMonths(months).AddDays(-1); }
        catch (ArgumentOutOfRangeException) { throw new ArgumentException("Choose a forecast range within supported calendar dates."); }
        var result = new List<BudgetIncomePeriodResponse> { first };
        while (result[^1].EndDate < target)
        {
            var last = result[^1];
            if (last.NextPayday is null || last.NextPayday <= last.StartDate || result.Count >= 380)
                throw new ArgumentException("The published income schedule does not cover this forecast range.");
            var next = await GetPeriod(last.NextPayday,null,published,calendars);
            if (next.Plan is null || next.StartDate != last.NextPayday || next.EndDate is null)
                throw new ArgumentException("The published income schedule does not cover this forecast range.");
            result.Add(next);
        }
        return result;
    }
}
