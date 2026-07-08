using Coiny.API.Data;
using Coiny.API.DTOs.Transactions;
using Coiny.API.Interfaces;
using Coiny.API.Models;
using Microsoft.EntityFrameworkCore;

namespace Coiny.API.Services;

public class TransactionService : ITransactionService
{
    private readonly CoinyContext _context;
    private readonly ICurrentUserService _currentUserService;

    public TransactionService (CoinyContext context, ICurrentUserService currentUserService)
    {
        _context = context; 
        _currentUserService = currentUserService;
    }

    public async Task<List<TransactionResponse>> GetTransactionsAsync()
    {
        var householdId = await _currentUserService.GetCurrentHouseholdIdAsync();

        return await _context.Transactions
            .Where(t => t.HouseholdId == householdId)
            .Include(t => t.Account)
            .Include(t => t.Category)
            .OrderByDescending(t => t.TransactionDate)
            .ThenByDescending(t => t.Id)
            .Select(t => new TransactionResponse
            {
                Id = t.Id,
                InstitutionName = t.Account.Institution != null
                    ? t.Account.Institution.Name
                    : null,
                AccountName = t.Account.Name,
                CategoryName = t.Category.Name,
                CategoryType = t.Category.CategoryType,
                Amount = t.Amount,
                TransactionDate = t.TransactionDate,
                Description = t.Description,
                Merchant = t.Merchant,
                IsCleared = t.IsCleared
            })
            .ToListAsync();
    }

    public async Task<TransactionResponse> CreateTransactionAsync(CreateTransactionRequest request)
    {
        var householdId = await _currentUserService.GetCurrentHouseholdIdAsync();

        var account = await _context.Accounts
            .FirstOrDefaultAsync(a => 
                a.Id == request.AccountId &&
                a.HouseholdId == householdId);

        if (account is null)
            throw new InvalidOperationException("Account does not belong to the current household");

        var category = await _context.Categories
            .FirstOrDefaultAsync(c => 
                c.Id == request.CategoryId &&
                c.HouseholdId == householdId);

        if (category is null)
            throw new InvalidOperationException("Category does not belong to the current household.");

        var transaction = new Transaction
        {
            HouseholdId = householdId,
            AccountId = account.Id,
            CategoryId = category.Id,
            Amount = request.Amount,
            TransactionDate = request.TransactionDate,
            Description = request.Description,
            Merchant = request.Merchant,
            Notes = request.Notes,
            IsCleared = request.IsCleared
        };

        _context.Transactions.Add(transaction);
        await _context.SaveChangesAsync();

        return new TransactionResponse
        {
            Id = transaction.Id,
            AccountName = account.Name,
            CategoryName = category.Name,
            CategoryType = category.CategoryType,
            Amount = transaction.Amount,
            TransactionDate = transaction.TransactionDate,
            Description = transaction.Description,
            Merchant = transaction.Merchant,
            IsCleared = transaction.IsCleared
        };
    }
}