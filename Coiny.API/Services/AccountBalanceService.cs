using Coiny.API.Data;
using Coiny.API.Enums;
using Coiny.API.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Coiny.API.Services;

public class AccountBalanceService : IAccountBalanceService
{
    private readonly IFinancialContext _context;

    public AccountBalanceService(IFinancialContext context)
    {
        _context = context;
    }

    public async Task<decimal> GetCurrentBalanceAsync(int accountId)
    {
        var account = await _context.Accounts
            .FirstAsync(a => a.Id == accountId);

        var transactions = await _context.Transactions
            .Where(t => t.AccountId == accountId)
            .Include(t => t.Category)
            .ToListAsync();

        decimal balance = account.OpeningBalance;

        foreach (var transaction in transactions)
        {
            balance += AccountBalanceCalculator.GetBalanceChange(
                account.AccountType, transaction.Category.CategoryType, transaction.Amount, transaction.TransferDirection);
        }

        return balance;
    }
}
