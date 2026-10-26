using Coiny.API.DTOs.Budgets;
using Coiny.API.Enums;
using Microsoft.EntityFrameworkCore;
namespace Coiny.API.Services;
public partial class BudgetActualsService
{
    public async Task<AvailableCashResponse> GetAvailableCashAsync(AvailableCashRequest request)
    {
        var household = await user.GetCurrentHouseholdIdAsync();
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.GetUtcNow(), TimeZoneInfo.FindSystemTimeZoneById("America/New_York")).DateTime);
        var selectedPeriod = await budgets.GetIncomePeriodAsync(request.AsOf);
        if (selectedPeriod.EndDate is not DateOnly end || selectedPeriod.Plan is null || end < today)
            throw new ArgumentException("Available cash uses today's balances. Choose the current or a future income period.");
        if (end > today.AddMonths(12)) throw new ArgumentException("Choose a period ending within the next 12 months.");
        var accounts = await context.Accounts.AsNoTracking().Where(a => a.HouseholdId == household && a.OpeningBalanceDate <= today &&
            (a.AccountType == AccountType.Checking || a.AccountType == AccountType.Savings || a.AccountType == AccountType.Cash || a.AccountType == AccountType.CreditCard))
            .OrderBy(a => a.Name).ToListAsync();
        var ids = accounts.Select(a => a.Id).ToArray();
        var cutoff = today.ToDateTime(TimeOnly.MaxValue);
        var entries = await context.Transactions.AsNoTracking().Include(t => t.Category)
            .Where(t => t.HouseholdId == household && ids.Contains(t.AccountId) && t.TransactionDate <= cutoff).ToListAsync();
        var balances = accounts.Select(a => new AvailableCashAccount(a.Id,a.Name,a.AccountType,
            a.OpeningBalance + entries.Where(t => t.AccountId == a.Id).Sum(t => AccountBalanceCalculator.GetBalanceChange(a.AccountType,t.Category.CategoryType,t.Amount,t.TransferDirection)),a.IsActive)).ToList();
        var selected = request.AccountIds ?? balances.Where(a => a.IsActive && a.AccountType is AccountType.Checking or AccountType.CreditCard).Select(a => a.Id).ToArray();
        if (selected.Length > 200 || selected.Distinct().Count() != selected.Length || selected.Any(id => !ids.Contains(id)))
            throw new ArgumentException("Choose different cash or credit-card accounts from your household. Reset accounts if a saved account was removed.");
        var chosen = balances.Where(a => selected.Contains(a.Id)).ToList();
        var cash = chosen.Where(a => a.AccountType != AccountType.CreditCard).Sum(a => a.Balance);
        // A credit on a card increases available funds; positive balances are amounts owed.
        var cards = chosen.Where(a => a.AccountType == AccountType.CreditCard).Sum(a => a.Balance);
        var current = await budgets.GetIncomePeriodAsync(today);
        var months = end <= current.EndDate ? 0 : new[] { 1,3,6,12 }.FirstOrDefault(m => current.StartDate!.Value.AddMonths(m) > end);
        if (months == 0 && end > current.EndDate) throw new ArgumentException("Choose a shorter forecast range.");
        var forecast = await GetForecastAsync(today,months);
        var anchor = forecast.Points.Single(p => p.Date == today);
        var finish = forecast.Points.Single(p => p.Date == end);
        var anchorRemainder = anchor.Actual ?? throw new ArgumentException("Today's actuals are unavailable. Refresh the budget and try again.");
        var futureChange = (finish.Forecast ?? finish.Actual ?? throw new ArgumentException("The forecast is unavailable.")) - anchorRemainder;
        // Remaining income is period expected income less income already recorded through today.
        // Only include periods reached by the selected end date, even if the forecast covers longer.
        var periods = forecast.Periods.Where(p => p.EndDate <= end).ToList();
        var from = forecast.StartDate.ToDateTime(TimeOnly.MinValue);
        var recordedIncome = await context.Transactions.AsNoTracking().Where(t => t.HouseholdId == household && t.Category.CategoryType == CategoryType.Income && t.TransactionDate >= from && t.TransactionDate <= cutoff)
            .Select(t => t.Amount).ToListAsync();
        var expectedIncome = periods.Sum(p => p.ForecastIncome) - recordedIncome.Sum();
        var remaining = expectedIncome - futureChange;
        return new(today,end,cash,cards,expectedIncome,remaining,
            chosen.Any(a => a.AccountType != AccountType.CreditCard) ? cash - cards + futureChange : null,selected,balances);
    }
}
