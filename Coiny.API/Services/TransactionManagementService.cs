using Coiny.API.Data;
using Coiny.API.DTOs.Transactions;
using Coiny.API.Enums;
using Coiny.API.Interfaces;
using Coiny.API.Models;
using Microsoft.EntityFrameworkCore;
namespace Coiny.API.Services;

public sealed class TransactionNotFoundException() : Exception("Transaction not found in your household.");
public sealed class TransactionConflictException(string message) : Exception(message);

public class TransactionManagementService(CoinyContext context, ICurrentUserService user, IAccountBalanceSnapshotService snapshots)
{
    public async Task<TransactionEditDetails> GetAsync(int id)
    {
        var household = await user.GetCurrentHouseholdIdAsync();
        var entry = await context.Transactions.AsNoTracking().Where(t => t.Id == id && t.HouseholdId == household)
            .Select(TransactionProjection.Response).SingleOrDefaultAsync() ?? throw new TransactionNotFoundException();
        var pairVersion = entry.PairedTransactionId is int pairId
            ? await context.Transactions.Where(t => t.Id == pairId && t.HouseholdId == household).Select(t => (int?)t.EditVersion).SingleOrDefaultAsync() : null;
        var bill = await context.BillPayments.AsNoTracking().Where(p => ((p.TransactionId == id || p.Links.Any(l => l.TransactionId == id)) || entry.PairedTransactionId.HasValue && (p.TransactionId == entry.PairedTransactionId || p.Links.Any(l => l.TransactionId == entry.PairedTransactionId))) && p.HouseholdId == household)
            .Select(p => new TransactionBillLink(p.AllocationKey, p.DueDate, p.IsPaid)).SingleOrDefaultAsync();
        return new(entry, pairVersion, bill);
    }
    public async Task<TransactionEditDetails> UpdateAsync(int id, UpdateTransactionRequest request)
    {
        var household = await user.GetCurrentHouseholdIdAsync();
        await using var unit = await context.Database.BeginTransactionAsync();
        var entry = await Owned(id, household);
        var pair = await Pair(entry, household);
        CheckVersions(entry, pair, request.ExpectedEditVersion, request.ExpectedPairedEditVersion);
        if (request.CategoryType is null || !Enum.IsDefined(request.CategoryType.Value) || request.Date is null || request.Date == DateOnly.MinValue ||
            request.Amount <= 0 || request.Amount > 9_999_999_999_999_999.99m || decimal.Round(request.Amount, 2) != request.Amount ||
            string.IsNullOrWhiteSpace(request.Description) || request.Description.Trim().Length > 250 || request.Merchant?.Length > 150 || request.Notes?.Length > 1000)
            throw new ArgumentException("Check the type, date, description, and positive amount with up to two decimal places.");
        var account = await context.Accounts.SingleOrDefaultAsync(a => a.Id == request.AccountId && a.HouseholdId == household)
            ?? throw new ArgumentException("Choose an account in your household.");
        if (!account.IsActive && account.Id != entry.AccountId) throw new ArgumentException("Choose an active destination account.");
        if (request.Date < account.OpeningBalanceDate) throw new ArgumentException("The date cannot be before the account's opening balance date.");
        if (pair is not null && (request.CategoryType != CategoryType.Transfer || request.Direction != entry.TransferDirection || request.AccountId == pair.AccountId))
            throw new ArgumentException("A linked transfer must keep its direction and use two different accounts. Unlink it before changing its type.");
        if (request.CategoryType != entry.Category.CategoryType && await context.BillPayments.AnyAsync(p => (p.TransactionId == id || p.Links.Any(l => l.TransactionId == id))))
            throw new TransactionConflictException("Unlink this transaction from its scheduled bill or contribution before changing its type.");
        if (await context.Set<SavingsActivity>().AnyAsync(a => !a.Kind.StartsWith("Voided:") && (a.TransactionId == id || pair != null && a.TransactionId == pair.Id)) &&
            (entry.Amount != request.Amount || entry.AccountId != request.AccountId || DateOnly.FromDateTime(entry.TransactionDate) != request.Date || entry.Category.CategoryType != request.CategoryType || entry.TransferDirection != request.Direction))
            throw new TransactionConflictException("Correct this movement’s savings fund activity before changing its amount, date, account, or type.");
        Category category;
        if (request.CategoryType == CategoryType.Transfer)
        {
            if (request.Direction is null || !Enum.IsDefined(request.Direction.Value) || (pair is null && request.Counterparty is not (TransferCounterparty.Unknown or TransferCounterparty.External)))
                throw new ArgumentException("Choose the transfer direction and an unknown or external counterparty.");
            category = await context.Categories.FirstOrDefaultAsync(c => c.HouseholdId == household && c.CategoryType == CategoryType.Transfer && c.IsActive)
                ?? new Category { HouseholdId = household, Name = "Transfer", CategoryType = CategoryType.Transfer, IsSystem = true, IsActive = true };
            if (category.Id == 0) {
                var names = await context.Categories.Where(c => c.HouseholdId == household).Select(c => c.Name).ToListAsync();
                for (var suffix = 2; names.Contains(category.Name, StringComparer.OrdinalIgnoreCase); suffix++) category.Name = $"Transfer ({suffix})";
                context.Categories.Add(category); await context.SaveChangesAsync();
            }
        }
        else
        {
            category = await context.Categories.SingleOrDefaultAsync(c => c.Id == request.CategoryId && c.HouseholdId == household && c.CategoryType == request.CategoryType)
                ?? throw new ArgumentException("Choose a category of the matching type from your household.");
            if (!category.IsActive && category.Id != entry.CategoryId) throw new ArgumentException("Choose an active category.");
        }
        var affected = new Dictionary<int, DateOnly>();
        var dates = new HashSet<(int AccountId, DateOnly Date)>();
        void Affect(int accountId, DateOnly date) { dates.Add((accountId, date)); if (!affected.TryGetValue(accountId, out var prior) || date < prior) affected[accountId] = date; }
        Affect(entry.AccountId, DateOnly.FromDateTime(entry.TransactionDate)); Affect(account.Id, request.Date.Value);
        if (pair is not null && pair.Amount != request.Amount) { Affect(pair.AccountId, DateOnly.FromDateTime(pair.TransactionDate)); pair.Amount = request.Amount; pair.EditVersion++; }
        entry.AccountId = account.Id; entry.CategoryId = category.Id; entry.Amount = request.Amount;
        entry.TransactionDate = request.Date.Value.ToDateTime(TimeOnly.MinValue); entry.Description = request.Description.Trim();
        entry.Merchant = Clean(request.Merchant); entry.Notes = Clean(request.Notes); entry.IsCleared = request.IsCleared;
        entry.TransferDirection = request.CategoryType == CategoryType.Transfer ? request.Direction : null;
        entry.TransferCounterparty = request.CategoryType == CategoryType.Transfer ? pair is null ? request.Counterparty : TransferCounterparty.Linked : null;
        entry.EditVersion++;
        // Keep a closing point at each old/new ledger date, including dates later than existing history.
        foreach (var (accountId, date) in dates)
            if (!await context.AccountBalanceSnapshots.AnyAsync(s => s.AccountId == accountId && s.SnapshotDate == date))
                context.AccountBalanceSnapshots.Add(new() { AccountId = accountId, SnapshotDate = date });
        try {
            await context.SaveChangesAsync();
            foreach (var (accountId, date) in affected) await snapshots.RecalculateFromAsync(accountId, date);
            await context.SaveChangesAsync(); await unit.CommitAsync();
        } catch (DbUpdateConcurrencyException) { throw Conflict(); }
        return await GetAsync(id);
    }
    public async Task DeleteAsync(int id, int expectedEditVersion, int? expectedPairedEditVersion)
    {
        var household = await user.GetCurrentHouseholdIdAsync();
        await using var unit = await context.Database.BeginTransactionAsync();
        var entry = await Owned(id, household); var pair = await Pair(entry, household);
        CheckVersions(entry, pair, expectedEditVersion, expectedPairedEditVersion);
        var entries = pair is null ? new[] { entry } : new[] { entry, pair };
        var affected = entries.GroupBy(t => t.AccountId).ToDictionary(g => g.Key, g => g.Min(t => DateOnly.FromDateTime(t.TransactionDate)));
        try {
            if (pair is not null) {
                foreach (var item in entries) { item.PairedTransactionId = null; item.PairedTransaction = null; item.TransferCounterparty = TransferCounterparty.Unknown; item.EditVersion++; }
                await context.SaveChangesAsync();
            }
            // BillPayment's SET NULL foreign key retains the independent paid status.
            context.Transactions.RemoveRange(entries); await context.SaveChangesAsync();
            foreach (var (accountId, date) in affected) await snapshots.RecalculateFromAsync(accountId, date);
            await context.SaveChangesAsync(); await unit.CommitAsync();
        } catch (DbUpdateConcurrencyException) { throw Conflict(); }
    }
    private async Task<Transaction> Owned(int id, int household) =>
        await context.Transactions.Include(t => t.Category).SingleOrDefaultAsync(t => t.Id == id && t.HouseholdId == household) ?? throw new TransactionNotFoundException();
    private async Task<Transaction?> Pair(Transaction entry, int household)
    {
        if (entry.PairedTransactionId is not int id) return null;
        var pair = await context.Transactions.SingleOrDefaultAsync(t => t.Id == id && t.HouseholdId == household);
        if (pair is null || pair.PairedTransactionId != entry.Id) throw Conflict();
        return pair;
    }
    private static void CheckVersions(Transaction entry, Transaction? pair, int version, int? pairedVersion)
    {
        if (version < 0 || entry.EditVersion != version || pair?.EditVersion != pairedVersion) throw Conflict();
    }
    private static TransactionConflictException Conflict() => new("This transaction changed. Reload its latest details before saving or deleting.");
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
