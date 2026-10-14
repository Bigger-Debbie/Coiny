using Coiny.API.Data;
using Coiny.API.DTOs.Transactions;
using Coiny.API.Interfaces;
using Coiny.API.Models;
using Coiny.API.Enums;
using Microsoft.EntityFrameworkCore;

namespace Coiny.API.Services;

public class TransactionService : ITransactionService
{
    private readonly CoinyContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IAccountBalanceSnapshotService _snapshotService;

    public TransactionService (CoinyContext context, ICurrentUserService currentUserService,
        IAccountBalanceSnapshotService snapshotService)
    {
        _context = context; 
        _currentUserService = currentUserService;
        _snapshotService = snapshotService;
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
        await using var databaseTransaction = await _context.Database.BeginTransactionAsync();

        var account = await _context.Accounts
            .FirstOrDefaultAsync(a => 
                a.Id == request.AccountId &&
                a.HouseholdId == householdId);

        if (account is null)
            throw new InvalidOperationException("Account does not belong to the current household");

        if (DateOnly.FromDateTime(request.TransactionDate) < account.OpeningBalanceDate)
            throw new InvalidOperationException("Transaction date cannot be before the account's opening balance date.");

        var category = await _context.Categories
            .FirstOrDefaultAsync(c => 
                c.Id == request.CategoryId &&
                c.HouseholdId == householdId);

        if (category is null)
            throw new InvalidOperationException("Category does not belong to the current household.");

        if (category.CategoryType == CategoryType.Transfer)
            throw new InvalidOperationException("Transfers require paired account transactions and are not supported yet.");

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
        await _snapshotService.RecalculateFromAsync(account.Id, DateOnly.FromDateTime(transaction.TransactionDate));
        await _context.SaveChangesAsync();
        await databaseTransaction.CommitAsync();

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
