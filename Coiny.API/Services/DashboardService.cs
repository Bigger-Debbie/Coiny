using Coiny.API.DTOs.Dashboard;
using Coiny.API.DTOs.Trends;
using Coiny.API.Enums;
using Coiny.API.Interfaces;
using Coiny.API.Models;

namespace Coiny.API.Services;

public class DashboardService : IDashboardService
{
    private readonly IAccountService _accountService;
    private readonly ITransactionService _transactionService;
    private readonly IAccountTrendService _accountTrendService;
    private readonly TimeProvider _timeProvider;
    private readonly UserPreferencesService _preferences;

    public DashboardService(IAccountService accountService, ITransactionService transactionService,
        IAccountTrendService accountTrendService, TimeProvider timeProvider, UserPreferencesService preferences)
    {
        _accountService = accountService;
        _transactionService = transactionService;
        _accountTrendService = accountTrendService;
        _timeProvider = timeProvider; _preferences = preferences;
    }

    public async Task<DashboardSummaryResponse> GetSummaryAsync()
    {
        var includeMortgage = (await _preferences.GetAsync()).IncludeMortgageInNetWorth;
        var accounts = await _accountService.GetAccountsAsync();
        var transactions = await _transactionService.GetTransactionsAsync();

        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(
            _timeProvider.GetUtcNow(), TimeZoneInfo.FindSystemTimeZoneById("America/New_York")).DateTime);
        var startDate = today.AddMonths(-1);
        var dashboardAccounts = new List<DashboardAccountsResponse>();

        foreach (var account in accounts)
        {
            // Keep the period fixed regardless of the hero chart selection.
            // EF shares a scoped context, so these queries run sequentially.
            var trend = await _accountTrendService.GetTrendAsync(startDate, today, [account.Id]);
            dashboardAccounts.Add(new DashboardAccountsResponse
            {
                Id = account.Id,
                Name = account.Name,
                AccountType = account.AccountType, LoanSubtype = account.LoanSubtype,
                CurrentBalance = account.CurrentBalance,
                InstitutionName = account.InstitutionName,
                MonthlyTrend = new DashboardAccountTrendResponse
                {
                    StartDate = startDate,
                    EndDate = today,
                    AbsoluteChange = trend.AbsoluteChange,
                    PercentageChange = trend.PercentageChange,
                    Direction = trend.Direction
                }
            });
        }

        var NetWorthSummary = new NetWorthSummaryResponse
        {
            IncludeMortgageInNetWorth = includeMortgage,
            HasMortgage = accounts.Any(a => a.AccountType == AccountType.Loan && a.LoanSubtype == LoanSubtype.Mortgage),
            Mortgage = accounts.Where(a => a.AccountType == AccountType.Loan && a.LoanSubtype == LoanSubtype.Mortgage).Sum(a => a.CurrentBalance),
            CurrentValue = dashboardAccounts.Where(a => includeMortgage || a.LoanSubtype != LoanSubtype.Mortgage).Sum(account =>
                IsLiability(account.AccountType)
                    ? -account.CurrentBalance
                    : account.CurrentBalance),

            Retirement = dashboardAccounts
                .Where(account =>
                    account.AccountType == AccountType.Retirement)
                .Sum(account => account.CurrentBalance),

            LiquidFunds = dashboardAccounts
                .Where(account =>
                    IsLiquidFund(account.AccountType))
                .Sum(account => account.CurrentBalance),

            Debt = dashboardAccounts
                .Where(account =>
                    IsLiability(account.AccountType) && account.LoanSubtype != LoanSubtype.Mortgage)
                .Sum(account => account.CurrentBalance)
        };

        var recentTransactions = transactions
            .OrderByDescending(transaction => transaction.TransactionDate)
            .ThenByDescending(transaction => transaction.Id)
            .Take(10)
            .Select(transaction => new DashboardTransactionResponse
            {
                Id = transaction.Id,
                AccountName = transaction.AccountName,
                AccountType = transaction.AccountType,
                InstitutionName = transaction.InstitutionName,
                CategoryName = transaction.CategoryName,
                CategoryType = transaction.CategoryType,
                Amount = transaction.Amount,
                TransactionDate = transaction.TransactionDate,
                Description = transaction.Description,
                Merchant = transaction.Merchant,
                TransferDirection = transaction.TransferDirection,
                TransferCounterparty = transaction.TransferCounterparty,
                PairedTransactionId = transaction.PairedTransactionId,
                CounterpartyAccountId = transaction.CounterpartyAccountId,
                CounterpartyAccountName = transaction.CounterpartyAccountName,
                CounterpartyAccountType = transaction.CounterpartyAccountType
            })
            .ToList();

        return new DashboardSummaryResponse
        {
            NetWorth = NetWorthSummary,
            Accounts = dashboardAccounts,
            RecentTransactions = recentTransactions
        };
    }

    public async Task<TrendResult> GetNetWorthTrendAsync(DateOnly startDate, DateOnly endDate)
    {
        var includeMortgage = (await _preferences.GetAsync()).IncludeMortgageInNetWorth;
        var ids = includeMortgage ? null : (await _accountService.GetAccountsAsync())
            .Where(a => a.LoanSubtype != LoanSubtype.Mortgage).Select(a => a.Id).ToArray();
        return await _accountTrendService.GetTrendAsync(startDate, endDate, ids,
            aggregation: AccountTrendAggregation.NetWorth);
    }

    private static bool IsLiability(AccountType accountType)
    {
        return accountType is
            AccountType.CreditCard or
            AccountType.Loan;
    }

    private static bool IsLiquidFund(AccountType accountType)
    {
        return accountType is
            AccountType.Checking or
            AccountType.Savings or
            AccountType.Cash;
    }
}
