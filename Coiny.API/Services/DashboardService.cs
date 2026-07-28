using Coiny.API.DTOs.Dashboard;
using Coiny.API.Enums;
using Coiny.API.Interfaces;
using Coiny.API.Models;

namespace Coiny.API.Services;

public class DashboardService : IDashboardService
{
    private readonly IAccountService _accountService;
    private readonly ITransactionService _transactionService;

    public DashboardService(IAccountService accountService, ITransactionService transactionService)
    {
        _accountService = accountService;
        _transactionService = transactionService;
    }

    public async Task<DashboardSummaryResponse> GetSummaryAsync()
    {
        var accounts = await _accountService.GetAccountsAsync();
        var transactions = await _transactionService.GetTransactionsAsync();

        var dashboardAccounts = accounts
            .Select(account => new DashboardAccountsResponse
            {
                Id = account.Id,
                Name = account.Name,
                AccountType = account.AccountType,
                CurrentBalance = account.CurrentBalance,
                InstitutionName = account.InstitutionName
            })
            .ToList();

        var NetWorthSummary = new NetWorthSummaryResponse
        {
            CurrentValue = dashboardAccounts.Sum(account => 
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
                    IsLiability(account.AccountType))
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
                InstitutionName = transaction.InstitutionName,
                CategoryName = transaction.CategoryName,
                CategoryType = transaction.CategoryType,
                Amount = transaction.Amount,
                TransactionDate = transaction.TransactionDate,
                Description = transaction.Description,
                Merchant = transaction.Merchant
            })
            .ToList();

        return new DashboardSummaryResponse
        {
            NetWorth = NetWorthSummary,
            Accounts = dashboardAccounts,
            RecentTransactions = recentTransactions
        };
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