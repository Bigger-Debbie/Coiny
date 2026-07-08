using Coiny.API.DTOs.Dashboard;
using Coiny.API.Enums;
using Coiny.API.Interfaces;
using Coiny.API.Models;

namespace Coiny.API.Services;

public class DashboardService : IDashboardService
{
    private readonly IAccountService _accountService;
    private readonly ITransactionService _transactionService;

    public DashboardService(
        IAccountService accountService,
        ITransactionService transactionService
    )
    {
        _accountService = accountService;
        _transactionService = transactionService;
    }

    public async Task<DashboardSummaryResponse> GetSummaryAsync()
    {
        var accounts = await _accountService.GetAccountsAsync();
        var transactions = await _transactionService.GetTransactionsAsync();

        var dashboardAccounts = accounts.Select(account =>
            new DashboardAccountsResponse
            {
                Id = account.Id,
                Name = account.Name,
                AccountType = account.AccountType,
                CurrentBalance = account.CurrentBalance,
                InstitutionName = account.InstitutionName
            })
            .ToList();

        var netWorth = dashboardAccounts.Sum(account =>
            IsLiability(account.AccountType)
                ? -account.CurrentBalance
                : account.CurrentBalance);

        var recentTransactions = transactions
            .OrderBy(t => t.TransactionDate)
            .ThenByDescending(t => t.Id)
            .Take(10)
            .Select(t => new DashboardTransactionResponse
            {
                Id = t.Id,
                AccountName = t.AccountName,
                InstitutionName = t.InstitutionName,
                CategoryName = t.CategoryName,
                CategoryType = t.CategoryType,
                Amount = t.Amount,
                TransactionDate = t.TransactionDate,
                Description = t.Description,
                Merchant = t.Merchant
            })
            .ToList();

        return new DashboardSummaryResponse
        {
            NetWorth = netWorth,
            Accounts = dashboardAccounts,
            RecentTransactions = recentTransactions
        };
    }

    private static bool IsLiability(AccountType accountType)
    {
        return accountType is AccountType.CreditCard or AccountType.Loan;
    }
}