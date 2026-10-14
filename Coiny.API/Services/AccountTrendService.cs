using Coiny.API.Data;
using Coiny.API.DTOs.Trends;
using Coiny.API.Enums;
using Coiny.API.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Coiny.API.Services;

public class AccountTrendService : IAccountTrendService
{
    private static readonly TimeZoneInfo ApplicationTimeZone = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");
    private readonly CoinyContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IAccountBalanceSnapshotService _snapshotService;
    private readonly ITrendService _trendService;
    private readonly TimeProvider _timeProvider;

    public AccountTrendService(
        CoinyContext context,
        ICurrentUserService currentUserService,
        IAccountBalanceSnapshotService snapshotService,
        ITrendService trendService,
        TimeProvider timeProvider)
    {
        _context = context;
        _currentUserService = currentUserService;
        _snapshotService = snapshotService;
        _trendService = trendService;
        _timeProvider = timeProvider;
    }

    public async Task<TrendResult> GetTrendAsync(
        DateOnly startDate,
        DateOnly endDate,
        IEnumerable<int>? accountIds = null,
        AccountTrendAggregation aggregation = AccountTrendAggregation.SumBalances)
    {
        if (endDate < startDate)
            throw new ArgumentException("End date cannot be before start date.", nameof(endDate));

        var today = DateOnly.FromDateTime(
            TimeZoneInfo.ConvertTime(_timeProvider.GetUtcNow(), ApplicationTimeZone).DateTime);

        if (startDate > today)
            throw new ArgumentException("Start date cannot be after today.", nameof(startDate));

        if (endDate > today)
            endDate = today;

        if (!Enum.IsDefined(aggregation))
            throw new ArgumentException("Unsupported account trend aggregation.", nameof(aggregation));

        var householdId = await _currentUserService.GetCurrentHouseholdIdAsync();
        var requestedIds = accountIds?.Distinct().ToArray();
        var query = _context.Accounts.AsNoTracking()
            .Where(account => account.HouseholdId == householdId);

        if (requestedIds is not null)
            query = query.Where(account => requestedIds.Contains(account.Id));

        var accounts = await query.OrderBy(account => account.Id).ToListAsync();

        // Reject the entire request instead of silently omitting unauthorized IDs.
        if (requestedIds is not null && accounts.Count != requestedIds.Length)
            throw new InvalidOperationException("One or more accounts do not belong to the current household.");

        if (accounts.Count == 0 || accounts.All(account => account.OpeningBalanceDate > endDate))
            return _trendService.CalculateTrend(Array.Empty<TrendDataPoint>());

        var totals = new decimal[endDate.DayNumber - startDate.DayNumber + 1];

        foreach (var account in accounts)
        {
            if (account.OpeningBalanceDate > endDate)
                continue;

            var snapshots = await _snapshotService.GetHistoryAsync(account.Id, startDate, endDate);
            var snapshotIndex = 0;
            // Opening balance is the baseline until a closing snapshot replaces it.
            var balance = account.OpeningBalance;
            var sign = aggregation == AccountTrendAggregation.NetWorth &&
                account.AccountType is AccountType.CreditCard or AccountType.Loan
                ? -1m
                : 1m;

            for (var dayIndex = 0; dayIndex < totals.Length; dayIndex++)
            {
                var date = DateOnly.FromDayNumber(startDate.DayNumber + dayIndex);

                while (snapshotIndex < snapshots.Count && snapshots[snapshotIndex].Date <= date)
                    balance = snapshots[snapshotIndex++].Value;

                if (date >= account.OpeningBalanceDate)
                    totals[dayIndex] += sign * balance;
            }
        }

        var points = totals.Select((value, dayIndex) => new TrendDataPoint
        {
            Date = DateOnly.FromDayNumber(startDate.DayNumber + dayIndex),
            Value = value
        });

        return _trendService.CalculateTrend(points);
    }
}
