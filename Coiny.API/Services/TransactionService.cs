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
            .Select(TransactionProjection.Response)
            .ToListAsync();
    }

    public async Task<TransactionPageResponse> GetPageAsync(TransactionQuery request)
    {
        if (request.Page < 1 || request.PageSize is < 1 or > 100)
            throw new ArgumentException("Page must be positive and page size must be between 1 and 100.");
        if (request.StartDate.HasValue && request.EndDate.HasValue && request.EndDate < request.StartDate)
            throw new ArgumentException("End date cannot be before start date.");
        var householdId = await _currentUserService.GetCurrentHouseholdIdAsync();
        if (request.AccountId.HasValue && !await _context.Accounts.AnyAsync(a =>
            a.Id == request.AccountId && a.HouseholdId == householdId))
            throw new InvalidOperationException("Account does not belong to the current household.");
        if (request.CategoryId.HasValue && !await _context.Categories.AnyAsync(c =>
            c.Id == request.CategoryId && c.HouseholdId == householdId))
            throw new InvalidOperationException("Category does not belong to the current household.");

        var query = _context.Transactions.AsNoTracking().Where(t => t.HouseholdId == householdId);
        if (request.AccountId.HasValue) query = query.Where(t => t.AccountId == request.AccountId);
        if (request.CategoryId.HasValue) query = query.Where(t => t.CategoryId == request.CategoryId);
        if (request.CategoryType.HasValue) query = query.Where(t => t.Category.CategoryType == request.CategoryType);
        if (request.StartDate.HasValue)
        {
            var start = request.StartDate.Value.ToDateTime(TimeOnly.MinValue);
            query = query.Where(t => t.TransactionDate >= start);
        }
        if (request.EndDate.HasValue)
        {
            var end = request.EndDate.Value.ToDateTime(TimeOnly.MaxValue);
            query = query.Where(t => t.TransactionDate <= end);
        }
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim().ToLowerInvariant();
            query = query.Where(t => t.Description.ToLower().Contains(search) ||
                (t.Merchant != null && t.Merchant.ToLower().Contains(search)) ||
                t.Account.Name.ToLower().Contains(search) || t.Category.Name.ToLower().Contains(search));
        }
        var total = await query.CountAsync();
        var page = Math.Min(request.Page, Math.Max(1, (int)Math.Ceiling(total / (double)request.PageSize)));
        var items = await query.OrderByDescending(t => t.TransactionDate).ThenByDescending(t => t.Id)
            .Skip((page - 1) * request.PageSize).Take(request.PageSize)
            .Select(TransactionProjection.Response).ToListAsync();
        return new TransactionPageResponse { Items = items, TotalCount = total, Page = page, PageSize = request.PageSize };
    }

    public async Task<CategoryBreakdownResponse> GetCategoryBreakdownAsync(CategoryBreakdownQuery request)
    {
        if (!request.StartDate.HasValue || !request.EndDate.HasValue)
            throw new ArgumentException("Start date and end date are required.");
        if (request.EndDate < request.StartDate)
            throw new ArgumentException("End date cannot be before start date.");
        if (request.CategoryType is not (CategoryType.Income or CategoryType.Expense))
            throw new ArgumentException("Category type must be Income or Expense.");

        var householdId = await _currentUserService.GetCurrentHouseholdIdAsync();
        if (request.AccountId.HasValue && !await _context.Accounts.AnyAsync(a =>
            a.Id == request.AccountId && a.HouseholdId == householdId))
            throw new InvalidOperationException("Account does not belong to the current household.");

        var start = request.StartDate.Value.ToDateTime(TimeOnly.MinValue);
        var end = request.EndDate.Value.ToDateTime(TimeOnly.MaxValue);
        var query = _context.Transactions.AsNoTracking().Where(t =>
            t.HouseholdId == householdId && t.Category.CategoryType == request.CategoryType &&
            t.TransactionDate >= start && t.TransactionDate <= end);
        if (request.AccountId.HasValue) query = query.Where(t => t.AccountId == request.AccountId);

        // Aggregate the full matching ledger, independently of transaction-list pagination.
        var groups = await query.GroupBy(t => new { t.CategoryId, t.Category.Name })
            .Select(g => new { g.Key.CategoryId, CategoryName = g.Key.Name,
                Amount = g.Sum(t => t.Amount), TransactionCount = g.Count() })
            .ToListAsync();
        var total = groups.Sum(g => g.Amount);
        return new CategoryBreakdownResponse
        {
            CategoryType = request.CategoryType.Value,
            TotalAmount = total,
            TransactionCount = groups.Sum(g => g.TransactionCount),
            Categories = groups.OrderByDescending(g => g.Amount).ThenBy(g => g.CategoryName).ThenBy(g => g.CategoryId)
                .Select(g => new CategoryBreakdownItem
                {
                    CategoryId = g.CategoryId, CategoryName = g.CategoryName, Amount = g.Amount,
                    TransactionCount = g.TransactionCount, Percentage = total == 0 ? 0 : g.Amount / total * 100
                }).ToList()
        };
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
            throw new InvalidOperationException("Use the transfer endpoints to record transfer entries.");

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
            AccountId = account.Id,
            CategoryId = category.Id,
            InstitutionName = await _context.Institutions.Where(i => i.Id == account.InstitutionId)
                .Select(i => i.Name).FirstOrDefaultAsync(),
            AccountName = account.Name,
            AccountType = account.AccountType,
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
