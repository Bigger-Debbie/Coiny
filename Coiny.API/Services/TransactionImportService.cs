using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Coiny.API.Data;
using Coiny.API.DTOs.Transactions;
using Coiny.API.Enums;
using Coiny.API.Interfaces;
using Coiny.API.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Coiny.API.Services;

public sealed class ImportValidationException(TransactionImportValidation validation) : Exception("Review the flagged rows before importing.")
{ public TransactionImportValidation Validation { get; } = validation; }
public sealed class ImportConflictException() : Exception("This submission ID was already used for different data. Start a new reviewed batch.");

public class TransactionImportService(CoinyContext context, ICurrentUserService user,
    IAccountBalanceSnapshotService snapshots, IAccountBalanceService balances, TimeProvider clock) : ITransactionImportService
{
    public async Task<TransactionImportValidation> ValidateAsync(TransactionImportRequest request)
    {
        Headers(request);
        return await ValidateRows(request, await user.GetCurrentHouseholdIdAsync());
    }

    public async Task<TransactionImportResponse> ImportAsync(TransactionImportRequest request)
    {
        Headers(request);
        var householdId = await user.GetCurrentHouseholdIdAsync();
        var userId = user.GetUserId() ?? throw new UnauthorizedAccessException();
        var hash = Hash(request);
        await using var unit = await context.Database.BeginTransactionAsync();
        var existing = await context.TransactionImportBatches.AsNoTracking().SingleOrDefaultAsync(b =>
            b.HouseholdId == householdId && b.SubmissionId == request.SubmissionId);
        if (existing is not null) return Replay(existing, hash);
        var validation = await ValidateRows(request, householdId);
        if (!validation.CanImport) throw new ImportValidationException(validation);
        var categories = await context.Categories.Where(c => c.HouseholdId == householdId).ToListAsync();
        Category? transferCategory = null;
        if (request.Rows.Any(r => r.CategoryType == CategoryType.Transfer))
        {
            transferCategory = categories.FirstOrDefault(c => c.IsActive && c.CategoryType == CategoryType.Transfer);
            if (transferCategory is null)
            {
                var name = "Transfer";
                for (var suffix = 2; categories.Any(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase)); suffix++) name = $"Transfer ({suffix})";
                transferCategory = new Category { HouseholdId = householdId, Name = name, CategoryType = CategoryType.Transfer, IsActive = true, IsSystem = true };
                context.Categories.Add(transferCategory);
            }
        }
        var batch = new TransactionImportBatch
        {
            HouseholdId = householdId, AccountId = request.AccountId, SubmissionId = request.SubmissionId,
            PayloadHash = hash, Filename = request.Filename.Trim(), CreatedByUserId = userId,
            ImportedCount = request.Rows.Count, CreatedAt = clock.GetUtcNow().UtcDateTime,
            BalanceAfter = validation.ProjectedBalance!.Value,
        };
        context.TransactionImportBatches.Add(batch);
        foreach (var row in request.Rows)
        {
            var transfer = row.CategoryType == CategoryType.Transfer;
            context.Transactions.Add(new Transaction
            {
                HouseholdId = householdId, AccountId = request.AccountId, ImportBatch = batch, ImportRowId = row.RowId,
                Category = transfer ? transferCategory! : categories.Single(c => c.Id == row.CategoryId),
                Amount = row.Amount!.Value, TransactionDate = row.Date!.Value.ToDateTime(TimeOnly.MinValue),
                Description = row.Description!.Trim(), Merchant = NullTrim(row.Merchant), Notes = NullTrim(row.Notes), IsCleared = row.IsCleared,
                TransferDirection = transfer ? row.Direction : null,
                TransferCounterparty = transfer ? row.Counterparty : null,
            });
        }
        var dates = request.Rows.Select(r => r.Date!.Value).Distinct().ToArray();
        var storedDates = await context.AccountBalanceSnapshots.Where(s => s.AccountId == request.AccountId && dates.Contains(s.SnapshotDate))
            .Select(s => s.SnapshotDate).ToListAsync();
        foreach (var date in dates.Except(storedDates)) context.AccountBalanceSnapshots.Add(new AccountBalanceSnapshot { AccountId = request.AccountId, SnapshotDate = date });
        try
        {
            await context.SaveChangesAsync();
            // Rebuild once from the earliest imported date, rather than once per row.
            await snapshots.RecalculateFromAsync(request.AccountId, request.Rows.Min(r => r.Date!.Value));
            await context.SaveChangesAsync();
            await unit.CommitAsync();
            return Response(batch);
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqliteException { SqliteExtendedErrorCode: 2067 })
        {
            await unit.RollbackAsync();
            context.ChangeTracker.Clear();
            var replay = await context.TransactionImportBatches.AsNoTracking().SingleOrDefaultAsync(b => b.HouseholdId == householdId && b.SubmissionId == request.SubmissionId);
            if (replay is null) throw;
            return Replay(replay, hash);
        }
    }

    public async Task<IReadOnlyList<TransactionImportResponse>> HistoryAsync(int? accountId)
    {
        var householdId = await user.GetCurrentHouseholdIdAsync();
        if (accountId.HasValue && !await context.Accounts.AnyAsync(a => a.Id == accountId && a.HouseholdId == householdId))
            throw new ArgumentException("Account does not belong to the current household.");
        return await context.TransactionImportBatches.AsNoTracking()
            .Where(b => b.HouseholdId == householdId && (!accountId.HasValue || b.AccountId == accountId))
            .OrderByDescending(b => b.Id).Take(50)
            .Select(b => new TransactionImportResponse(b.Id, b.SubmissionId, b.AccountId, b.Filename, b.ImportedCount, DateTime.SpecifyKind(b.CreatedAt, DateTimeKind.Utc), b.BalanceAfter)).ToListAsync();
    }

    private async Task<TransactionImportValidation> ValidateRows(TransactionImportRequest request, int householdId)
    {
        var account = await context.Accounts.AsNoTracking().SingleOrDefaultAsync(a => a.Id == request.AccountId && a.HouseholdId == householdId && a.IsActive)
            ?? throw new ArgumentException("Choose an active account in your household.");
        var categories = await context.Categories.AsNoTracking().Where(c => c.HouseholdId == householdId && c.IsActive).ToDictionaryAsync(c => c.Id);
        var datedRows = request.Rows.Where(r => r.Date.HasValue).ToArray();
        var ledger = new Dictionary<EntryKey, List<int>>();
        if (datedRows.Length > 0)
        {
            var start = datedRows.Min(r => r.Date!.Value).ToDateTime(TimeOnly.MinValue);
            var end = datedRows.Max(r => r.Date!.Value).ToDateTime(TimeOnly.MaxValue);
            var existing = await context.Transactions.AsNoTracking().Where(t => t.AccountId == account.Id &&
                t.HouseholdId == householdId && t.TransactionDate >= start && t.TransactionDate <= end)
                .Select(t => new { t.Id, t.TransactionDate, t.Amount, t.Description, t.TransferDirection, t.Category.CategoryType }).ToListAsync();
            foreach (var item in existing)
            {
                var direction = item.CategoryType == CategoryType.Transfer ? item.TransferDirection :
                    item.CategoryType == CategoryType.Income ? TransferDirection.In : TransferDirection.Out;
                if (direction is null) continue;
                var key = new EntryKey(DateOnly.FromDateTime(item.TransactionDate), item.Amount, direction.Value, Normalize(item.Description));
                if (!ledger.TryGetValue(key, out var ids)) ledger[key] = ids = [];
                ids.Add(item.Id);
            }
        }
        var fileGroups = request.Rows.Where(r => Key(r) is not null).GroupBy(r => Key(r)!.Value)
            .ToDictionary(g => g.Key, g => g.Select(r => r.RowId).ToArray());
        var checks = new List<ImportRowCheck>();
        decimal delta = 0;
        foreach (var row in request.Rows)
        {
            var errors = new List<string>();
            if (!row.Date.HasValue || row.Date == DateOnly.MinValue || row.Date < account.OpeningBalanceDate) errors.Add("Date must be on or after the account's opening balance date.");
            if (row.Amount is not decimal amount || amount <= 0 || amount > 9999999999999999.99m || decimal.Round(amount, 2) != amount) errors.Add("Amount must be positive with at most two decimal places.");
            if (row.Direction is null || !Enum.IsDefined(row.Direction.Value)) errors.Add("Choose money in or money out.");
            if (string.IsNullOrWhiteSpace(row.Description) || row.Description.Length > 250) errors.Add("Enter a description of up to 250 characters.");
            if (row.Merchant?.Length > 150 || row.Notes?.Length > 1000) errors.Add("Merchant or notes are too long.");
            if (row.CategoryType == CategoryType.Transfer)
            {
                if (row.Counterparty is not (TransferCounterparty.Unknown or TransferCounterparty.External)) errors.Add("Imported transfers must be unlinked or external.");
                if (row.CategoryId.HasValue && (!categories.TryGetValue(row.CategoryId.Value, out var transfer) || transfer.CategoryType != CategoryType.Transfer)) errors.Add("Choose a household transfer category or leave its ID empty.");
            }
            else if (row.CategoryType is not (CategoryType.Income or CategoryType.Expense) || row.CategoryId is not int categoryId ||
                !categories.TryGetValue(categoryId, out var category) || category.CategoryType != row.CategoryType)
                errors.Add("Assign an active Income/Expense category from your household, or classify this as Transfer.");
            if ((row.CategoryType == CategoryType.Income && row.Direction != TransferDirection.In) ||
                (row.CategoryType == CategoryType.Expense && row.Direction != TransferDirection.Out)) errors.Add("Category type must match the money direction.");
            var key = Key(row);
            var duplicates = key.HasValue && ledger.TryGetValue(key.Value, out var ids) ? ids.Take(5).ToArray() : [];
            var repeats = key.HasValue && fileGroups.TryGetValue(key.Value, out var rows) ? rows.Where(id => id != row.RowId).Take(5).ToArray() : [];
            if ((duplicates.Length > 0 || repeats.Length > 0) && !row.AllowDuplicate) errors.Add("Possible duplicate: skip this row or explicitly choose Import anyway.");
            if (errors.Count == 0)
                delta += AccountBalanceCalculator.GetBalanceChange(account.AccountType, row.CategoryType!.Value, row.Amount!.Value,
                    row.CategoryType == CategoryType.Transfer ? row.Direction : null);
            checks.Add(new(row.RowId, errors, duplicates, repeats));
        }
        var current = await balances.GetCurrentBalanceAsync(account.Id);
        var valid = checks.All(c => c.Errors.Count == 0);
        return new(checks, valid, current, valid ? current + delta : null);
    }
    private static void Headers(TransactionImportRequest request)
    {
        if (request.SubmissionId == Guid.Empty) throw new ArgumentException("A nonempty submission ID is required.");
        if (request.AccountId < 1 || string.IsNullOrWhiteSpace(request.Filename) || request.Filename.Length > 255)
            throw new ArgumentException("Choose an account and a filename of up to 255 characters.");
        if (request.Rows is null || request.Rows.Count is < 1 or > 2000 || request.Rows.Any(r => r is null || r.RowId < 1) ||
            request.Rows.Select(r => r.RowId).Distinct().Count() != request.Rows.Count)
            throw new ArgumentException("Provide 1–2,000 rows with distinct positive row IDs.");
    }
    private static string Hash(TransactionImportRequest request) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
    {
        request.AccountId, Filename = request.Filename.Trim(), Rows = request.Rows.OrderBy(r => r.RowId).Select(r => new
        { r.RowId, r.Date, Amount = r.Amount?.ToString("G29", CultureInfo.InvariantCulture), r.Direction, r.CategoryType, r.CategoryId, r.Counterparty,
            Description = r.Description?.Trim(), Merchant = NullTrim(r.Merchant), Notes = NullTrim(r.Notes), r.IsCleared, r.AllowDuplicate }),
    }))));
    private static string? NullTrim(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static string Normalize(string value) => string.Join(' ', value.Normalize(NormalizationForm.FormKC).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).ToUpperInvariant();
    private readonly record struct EntryKey(DateOnly Date, decimal Amount, TransferDirection Direction, string Description);
    private static EntryKey? Key(TransactionImportRow row) => row.Date is DateOnly date && row.Amount is decimal amount && row.Direction is TransferDirection direction && Enum.IsDefined(direction)
        ? new(date, amount, direction, Normalize(row.Description ?? "")) : null;
    private static TransactionImportResponse Response(TransactionImportBatch batch) => new(batch.Id, batch.SubmissionId, batch.AccountId, batch.Filename, batch.ImportedCount, DateTime.SpecifyKind(batch.CreatedAt, DateTimeKind.Utc), batch.BalanceAfter);
    private static TransactionImportResponse Replay(TransactionImportBatch batch, string hash) => batch.PayloadHash == hash ? Response(batch) : throw new ImportConflictException();
}
