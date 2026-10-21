using Coiny.API.Data;
using Coiny.API.DTOs.Transactions;
using Coiny.API.Enums;
using Coiny.API.Interfaces;
using Coiny.API.Models;
using Microsoft.EntityFrameworkCore;

namespace Coiny.API.Services;

public sealed class TransferNotFoundException() : Exception("Transfer entry not found.");
public sealed class TransferConflictException(string message) : Exception(message);

public class TransferService(CoinyContext context, ICurrentUserService currentUser,
    IAccountBalanceSnapshotService snapshots) : ITransferService
{
    public async Task<List<TransactionResponse>> GetCandidatesAsync(int transactionId, int accountId, int days)
    {
        if (days is < 0 or > 365) throw new ArgumentException("Choose a date window between 0 and 365 days.");
        var household = await currentUser.GetCurrentHouseholdIdAsync();
        var source = await OwnedEntry(transactionId, household);
        if (source.PairedTransactionId.HasValue) throw new TransferConflictException("This entry is already linked. Reload before trying again.");
        if (source.AccountId == accountId) throw new ArgumentException("Choose a different account.");
        if (!await context.Accounts.AnyAsync(a => a.Id == accountId && a.HouseholdId == household)) throw new TransferNotFoundException();
        var date = DateOnly.FromDateTime(source.TransactionDate);
        var start = DateOnly.FromDayNumber(Math.Max(DateOnly.MinValue.DayNumber, date.DayNumber - days)).ToDateTime(TimeOnly.MinValue);
        var end = DateOnly.FromDayNumber(Math.Min(DateOnly.MaxValue.DayNumber, date.DayNumber + days)).ToDateTime(TimeOnly.MaxValue);
        var opposite = source.TransferDirection == TransferDirection.Out ? TransferDirection.In : TransferDirection.Out;
        var rows = await context.Transactions.AsNoTracking().Where(t => t.HouseholdId == household && t.AccountId == accountId &&
            t.Category.CategoryType == CategoryType.Transfer && t.PairedTransactionId == null && t.TransferDirection == opposite &&
            t.Amount == source.Amount && t.TransactionDate >= start && t.TransactionDate <= end)
            .Select(TransactionProjection.Response).ToListAsync();
        return rows.OrderBy(t => Math.Abs(DateOnly.FromDateTime(t.TransactionDate).DayNumber - date.DayNumber))
            .ThenBy(t => t.TransactionDate).ThenBy(t => t.Id).ToList();
    }

    public async Task<TransferPairResponse> CreateCounterpartAsync(int transactionId, CreateTransferCounterpartRequest request)
    {
        if (request.Date is not { } date || date == DateOnly.MinValue) throw new ArgumentException("Choose a posting date for the new entry.");
        var household = await currentUser.GetCurrentHouseholdIdAsync();
        await using var unit = await context.Database.BeginTransactionAsync();
        var source = await OwnedEntry(transactionId, household);
        CheckVersion(source, request.ExpectedEditVersion);
        if (source.PairedTransactionId.HasValue) throw new TransferConflictException("This entry is already linked. Reload before trying again.");
        if (source.AccountId == request.AccountId) throw new ArgumentException("Choose a different account.");
        var account = await Account(request.AccountId, household, date);
        var category = await Category(household);
        var other = Entry(account, category, source.Amount, date,
            source.TransferDirection == TransferDirection.Out ? TransferDirection.In : TransferDirection.Out,
            TransferCounterparty.Unknown, source.Description, null, false);
        context.Transactions.Add(other); await context.SaveChangesAsync();
        Pair(source, other); await context.SaveChangesAsync();
        await snapshots.RecalculateFromAsync(account.Id, date); await context.SaveChangesAsync();
        await unit.CommitAsync();
        var outgoing = source.TransferDirection == TransferDirection.Out ? source : other;
        var incoming = outgoing == source ? other : source;
        return new(await Response(outgoing.Id, household), await Response(incoming.Id, household));
    }

    private static void CheckVersion(Transaction entry, int? expected)
    {
        if (expected.HasValue && entry.EditVersion != expected.Value)
            throw new TransferConflictException("A transfer entry changed. Reload and review the match again.");
    }

    public async Task<TransferPairResponse> CreateAsync(CreateTransferRequest request)
    {
        Validate(request.Amount, request.Date, request.Description, request.Notes);
        if (request.FromAccountId == request.ToAccountId)
            throw new ArgumentException("Choose two different accounts.");
        var householdId = await currentUser.GetCurrentHouseholdIdAsync();
        await using var unit = await context.Database.BeginTransactionAsync();
        var from = await Account(request.FromAccountId, householdId, request.Date!.Value);
        var to = await Account(request.ToAccountId, householdId, request.Date.Value);
        var category = await Category(householdId);
        var outgoing = Entry(from, category, request.Amount, request.Date.Value, TransferDirection.Out,
            TransferCounterparty.Unknown, request.Description?.Trim() ?? "Transfer", request.Notes, request.IsCleared);
        var incoming = Entry(to, category, request.Amount, request.Date.Value, TransferDirection.In,
            TransferCounterparty.Unknown, request.Description?.Trim() ?? "Transfer", request.Notes, request.IsCleared);
        context.Transactions.AddRange(outgoing, incoming);
        await context.SaveChangesAsync();
        Pair(outgoing, incoming);
        await context.SaveChangesAsync();
        await snapshots.RecalculateFromAsync(from.Id, request.Date.Value);
        await snapshots.RecalculateFromAsync(to.Id, request.Date.Value);
        await context.SaveChangesAsync();
        await unit.CommitAsync();
        return new(await Response(outgoing.Id, householdId), await Response(incoming.Id, householdId));
    }

    public async Task<TransactionResponse> CreateEntryAsync(CreateTransferEntryRequest request)
    {
        Validate(request.Amount, request.Date, request.Description, request.Notes);
        if (string.IsNullOrWhiteSpace(request.Description)) throw new ArgumentException("Description is required.");
        if (request.Direction is null || !Enum.IsDefined(request.Direction.Value))
            throw new ArgumentException("Choose an incoming or outgoing direction.");
        if (request.Counterparty is not (TransferCounterparty.Unknown or TransferCounterparty.External))
            throw new ArgumentException("A single entry must have an unknown or external counterparty.");
        var householdId = await currentUser.GetCurrentHouseholdIdAsync();
        await using var unit = await context.Database.BeginTransactionAsync();
        var account = await Account(request.AccountId, householdId, request.Date!.Value);
        var category = await Category(householdId);
        var entry = Entry(account, category, request.Amount, request.Date.Value, request.Direction.Value,
            request.Counterparty, request.Description.Trim(), request.Notes, request.IsCleared);
        context.Transactions.Add(entry);
        await context.SaveChangesAsync();
        await snapshots.RecalculateFromAsync(account.Id, request.Date.Value);
        await context.SaveChangesAsync();
        await unit.CommitAsync();
        return await Response(entry.Id, householdId);
    }

    public async Task<TransferPairResponse> LinkAsync(LinkTransferRequest request)
    {
        if (request.FirstTransactionId == request.SecondTransactionId)
            throw new ArgumentException("Choose two different entries.");
        var householdId = await currentUser.GetCurrentHouseholdIdAsync();
        await using var unit = context.Database.CurrentTransaction is null ? await context.Database.BeginTransactionAsync() : null;
        var first = await OwnedEntry(request.FirstTransactionId, householdId);
        var second = await OwnedEntry(request.SecondTransactionId, householdId);
        CheckVersion(first, request.ExpectedFirstEditVersion); CheckVersion(second, request.ExpectedSecondEditVersion);
        if (first.PairedTransactionId.HasValue || second.PairedTransactionId.HasValue)
            throw new TransferConflictException("An entry is already linked. Reload before trying again.");
        if (first.AccountId == second.AccountId || first.Amount != second.Amount ||
            first.TransferDirection == second.TransferDirection)
            throw new ArgumentException("Transfers require equal amounts, opposite directions, and different accounts.");
        Pair(first, second);
        await context.SaveChangesAsync();
        if (unit is not null) await unit.CommitAsync();
        var outgoing = first.TransferDirection == TransferDirection.Out ? first : second;
        var incoming = outgoing == first ? second : first;
        return new(await Response(outgoing.Id, householdId), await Response(incoming.Id, householdId));
    }

    public async Task UnlinkAsync(int transactionId, int? expectedEditVersion = null, int? expectedPairedEditVersion = null)
    {
        var householdId = await currentUser.GetCurrentHouseholdIdAsync();
        await using var unit = await context.Database.BeginTransactionAsync();
        var entry = await OwnedEntry(transactionId, householdId);
        if (entry.PairedTransactionId is not int pairedId)
            throw new TransferConflictException("This entry is not linked.");
        var other = await OwnedEntry(pairedId, householdId);
        CheckVersion(entry, expectedEditVersion); CheckVersion(other, expectedPairedEditVersion);
        if (other.PairedTransactionId != entry.Id) throw new TransferConflictException("The transfer link has changed.");
        foreach (var item in new[] { entry, other })
        {
            item.EditVersion++;
            item.PairedTransactionId = null;
            item.PairedTransaction = null;
            item.TransferCounterparty = TransferCounterparty.Unknown;
        }
        await context.SaveChangesAsync();
        await unit.CommitAsync();
    }

    private async Task<Account> Account(int id, int householdId, DateOnly date)
    {
        var account = await context.Accounts.SingleOrDefaultAsync(a => a.Id == id && a.HouseholdId == householdId && a.IsActive)
            ?? throw new ArgumentException("Choose an active account in your household.");
        if (date < account.OpeningBalanceDate)
            throw new ArgumentException("Transfer date cannot be before either account's opening balance date.");
        return account;
    }
    private async Task<Category> Category(int householdId)
    {
        var existing = await context.Categories.FirstOrDefaultAsync(c => c.HouseholdId == householdId &&
            c.CategoryType == CategoryType.Transfer && c.IsActive);
        if (existing is not null) return existing;
        var names = await context.Categories.Where(c => c.HouseholdId == householdId).Select(c => c.Name).ToListAsync();
        var name = "Transfer";
        for (var suffix = 2; names.Contains(name, StringComparer.OrdinalIgnoreCase); suffix++) name = $"Transfer ({suffix})";
        var category = new Category { HouseholdId = householdId, Name = name, CategoryType = CategoryType.Transfer, IsActive = true, IsSystem = true };
        context.Categories.Add(category);
        return category;
    }
    private async Task<Transaction> OwnedEntry(int id, int householdId) => await context.Transactions
        .SingleOrDefaultAsync(t => t.Id == id && t.HouseholdId == householdId &&
            t.Category.CategoryType == CategoryType.Transfer && t.TransferDirection != null)
        ?? throw new TransferNotFoundException();

    private static Transaction Entry(Account account, Category category, decimal amount, DateOnly date,
        TransferDirection direction, TransferCounterparty counterparty, string description, string? notes, bool cleared) => new()
        {
            HouseholdId = account.HouseholdId, AccountId = account.Id, Category = category, Amount = amount,
            TransactionDate = date.ToDateTime(TimeOnly.MinValue), Description = string.IsNullOrWhiteSpace(description) ? "Transfer" : description,
            Notes = notes, IsCleared = cleared, TransferDirection = direction, TransferCounterparty = counterparty,
        };
    private static void Pair(Transaction first, Transaction second)
    {
        first.EditVersion++; second.EditVersion++;
        first.PairedTransactionId = second.Id; second.PairedTransactionId = first.Id;
        first.TransferCounterparty = second.TransferCounterparty = TransferCounterparty.Linked;
    }
    private static void Validate(decimal amount, DateOnly? date, string? description, string? notes)
    {
        if (amount <= 0 || amount > 9999999999999999.99m || decimal.Round(amount, 2) != amount)
            throw new ArgumentException("Amount must be positive with at most two decimal places.");
        if (date is null || date == DateOnly.MinValue) throw new ArgumentException("A transfer date is required.");
        if (description?.Length > 250 || notes?.Length > 1000) throw new ArgumentException("Description or notes are too long.");
    }
    private Task<TransactionResponse> Response(int id, int householdId) => context.Transactions.AsNoTracking()
        .Where(t => t.Id == id && t.HouseholdId == householdId).Select(TransactionProjection.Response).SingleAsync();
}
