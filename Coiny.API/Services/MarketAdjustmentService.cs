using Coiny.API.Data;
using Coiny.API.DTOs.Transactions;
using Coiny.API.Enums;
using Coiny.API.Interfaces;
using Coiny.API.Models;
using Microsoft.EntityFrameworkCore;
namespace Coiny.API.Services;

public class MarketAdjustmentService(IFinancialContext context, ICurrentUserService user, IAccountBalanceSnapshotService snapshots)
{
    internal static bool Eligible(AccountType type) => type is AccountType.Investment or AccountType.Retirement or AccountType.Crypto;
    internal static void ValidateMoney(decimal amount)
    {
        if (amount < 0 || amount > 9_999_999_999_999_999.99m || decimal.Round(amount, 2) != amount)
            throw new ArgumentException("Enter a non-negative balance with up to two decimal places.");
    }
    internal static async Task<decimal> BalanceAsync(IFinancialContext context, int household, int accountId, DateOnly date, int? excludingId = null)
    {
        var account = await context.Accounts.AsNoTracking().SingleOrDefaultAsync(a => a.Id == accountId && a.HouseholdId == household)
            ?? throw new ArgumentException("Choose an account in your household.");
        if (!Eligible(account.AccountType)) throw new ArgumentException("Market adjustments are available for investment, retirement, and crypto accounts.");
        if (date == DateOnly.MinValue || date < account.OpeningBalanceDate) throw new ArgumentException("Choose a date on or after the account's opening date.");
        var end = date.ToDateTime(TimeOnly.MaxValue);
        var entries = await context.Transactions.AsNoTracking().Where(t => t.AccountId == accountId && t.HouseholdId == household && t.TransactionDate <= end && (!excludingId.HasValue || t.Id != excludingId))
            .Select(t => new { t.Category.CategoryType, t.Amount, t.TransferDirection }).ToListAsync();
        return account.OpeningBalance + entries.Sum(t => AccountBalanceCalculator.GetBalanceChange(account.AccountType, t.CategoryType, t.Amount, t.TransferDirection));
    }
    public async Task<MarketAdjustmentBalance> GetBalanceAsync(int accountId, DateOnly date, int? excludingId)
    {
        var household = await user.GetCurrentHouseholdIdAsync();
        if (excludingId.HasValue && !await context.Transactions.AnyAsync(t => t.Id == excludingId && t.HouseholdId == household && t.Category.CategoryType == CategoryType.MarketAdjustment))
            throw new TransactionNotFoundException();
        return new(await BalanceAsync(context, household, accountId, date, excludingId));
    }
    internal static async Task<Category> CategoryAsync(IFinancialContext context, int household)
    {
        var category = await context.Categories.FirstOrDefaultAsync(c => c.HouseholdId == household && c.CategoryType == CategoryType.MarketAdjustment && c.IsActive);
        if (category is not null) return category;
        var names = await context.Categories.Where(c => c.HouseholdId == household).Select(c => c.Name).ToListAsync();
        var name = "Market adjustment";
        for (var suffix = 2; names.Contains(name, StringComparer.OrdinalIgnoreCase); suffix++) name = $"Market adjustment ({suffix})";
        category = new() { HouseholdId = household, Name = name, CategoryType = CategoryType.MarketAdjustment, IsSystem = true, IsActive = true };
        context.Categories.Add(category); await context.SaveChangesAsync(); return category;
    }
    public async Task<TransactionResponse> CreateAsync(MarketAdjustmentRequest request)
    {
        ValidateMoney(request.UpdatedBalance);
        if (request.Notes?.Length > 1000) throw new ArgumentException("Notes must be at most 1000 characters.");
        var household = await user.GetCurrentHouseholdIdAsync();
        await using var unit = await context.Database.BeginTransactionAsync();
        var balance = await BalanceAsync(context, household, request.AccountId, request.Date);
        if (!await context.Accounts.AnyAsync(a => a.Id == request.AccountId && a.HouseholdId == household && a.IsActive)) throw new ArgumentException("Choose an active account.");
        if (balance != request.ExpectedBalance) throw new TransactionConflictException("This account balance changed. Review the updated adjustment before saving.");
        var delta = request.UpdatedBalance - balance;
        if (delta == 0) throw new ArgumentException("The balance is unchanged; no market adjustment is needed.");
        if (Math.Abs(delta) > 9_999_999_999_999_999.99m) throw new ArgumentException("The adjustment is too large.");
        var category = await CategoryAsync(context, household);
        var entry = new Transaction { HouseholdId = household, AccountId = request.AccountId, CategoryId = category.Id,
            Amount = delta, TransactionDate = request.Date.ToDateTime(TimeOnly.MinValue), Description = delta > 0 ? "Market gain" : "Market loss",
            Notes = request.Notes?.Trim(), IsCleared = request.IsCleared };
        context.Transactions.Add(entry); await context.SaveChangesAsync();
        await snapshots.RecalculateFromAsync(entry.AccountId, request.Date); await context.SaveChangesAsync(); await unit.CommitAsync();
        return await context.Transactions.AsNoTracking().Where(t => t.Id == entry.Id).Select(TransactionProjection.Response).SingleAsync();
    }
}
