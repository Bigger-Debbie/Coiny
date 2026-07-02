using Coiny.API.Data;
using Coiny.API.Enums;
using Coiny.API.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Coiny.API.Services;

public class AccountBalanceService : IAccountBalanceService
{
    private readonly CoinyContext _context;

    public AccountBalanceService(CoinyContext context)
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
            switch (transaction.Category.CategoryType)
            {
                case CategoryType.Income:
                    balance += transaction.Amount;
                    break;
                
                case CategoryType.Expense:
                    balance -= transaction.Amount;
                    break;

                case CategoryType.Transfer:
                    // TODO: Add transfer logic
                    break;
            }
        }

        return balance;
    }
}