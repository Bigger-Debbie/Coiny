using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Coiny.API.Data;
using Coiny.API.DTOs.Transactions;
using Coiny.API.Enums;
using Coiny.API.Interfaces;
using Coiny.API.Models;
using Microsoft.EntityFrameworkCore;
namespace Coiny.API.Services;

public class ImportSessionService(IFinancialContext context, ICurrentUserService user, ITransactionImportService imports, ITransferService transfers, TimeProvider clock)
{
    public async Task<ImportSessionValidation> ValidateAsync(ImportSessionRequest request)
    {
        CheckHeaders(request);
        var household = await user.GetCurrentHouseholdIdAsync();
        var checks = new List<ImportSessionFileCheck>();
        foreach (var file in request.Files) checks.Add(new(file.AccountId, await imports.ValidateAsync(file)));
        if (checks.Any(c => !c.Validation.CanImport)) return new(false, checks, [], false);
        await ValidateLinks(request, household);
        var accounts = await context.Accounts.AsNoTracking().Where(a => a.HouseholdId == household).ToDictionaryAsync(a => a.Id);
        var fresh = request.Files.SelectMany(f => f.Rows.Where(r => r.CategoryType == CategoryType.Transfer).Select(r => new ImportSessionEntry(
            new(f.AccountId,r.RowId,null,null), accounts[f.AccountId].Name,r.Date!.Value,r.Amount!.Value,r.Direction!.Value,r.Description!))).ToList();
        if (fresh.Count == 0) return new(true,checks,[],false);
        var min = fresh.Min(r => r.Date).ToDateTime(TimeOnly.MinValue); var max = fresh.Max(r => r.Date).ToDateTime(TimeOnly.MaxValue);
        min = min < DateTime.MinValue.AddDays(7) ? DateTime.MinValue : min.AddDays(-7);
        max = max > DateTime.MaxValue.AddDays(-7) ? DateTime.MaxValue : max.AddDays(7);
        var amounts = fresh.Select(r => r.Amount).Distinct().ToArray();
        var existing = await context.Transactions.AsNoTracking().Where(t => t.HouseholdId == household && t.Category.CategoryType == CategoryType.Transfer &&
            t.PairedTransactionId == null && t.TransferDirection != null && amounts.Contains(t.Amount) && t.TransactionDate >= min && t.TransactionDate <= max)
            .Select(TransactionProjection.Response).ToListAsync();
        var all = fresh.Concat(existing.Select(t => new ImportSessionEntry(new(t.AccountId,null,t.Id,t.EditVersion),t.AccountName,DateOnly.FromDateTime(t.TransactionDate),t.Amount,t.TransferDirection!.Value,t.Description))).ToList();
        var buckets = all.GroupBy(r => (r.Amount,r.Direction)).ToDictionary(g => g.Key,g => g.ToList());
        var suggestions = new List<ImportSessionSuggestion>(); var seen = new HashSet<string>();
        foreach (var first in fresh) {
            var opposite = first.Direction == TransferDirection.In ? TransferDirection.Out : TransferDirection.In;
            if (!buckets.TryGetValue((first.Amount,opposite),out var candidates)) continue;
            foreach (var second in candidates.Where(s => s.Reference.AccountId != first.Reference.AccountId && Math.Abs(s.Date.DayNumber-first.Date.DayNumber) <= 7)
                .OrderBy(s => Math.Abs(s.Date.DayNumber-first.Date.DayNumber)).ThenBy(s => s.Date)) {
                var keys = new[] { Key(first.Reference),Key(second.Reference) }; Array.Sort(keys,StringComparer.Ordinal);
                if (!seen.Add(string.Join("|",keys))) continue;
                suggestions.Add(new(first,second));
                if (suggestions.Count == 201) return new(true,checks,suggestions.Take(200).ToList(),true);
            }
        }
        return new(true,checks,suggestions,false);
    }

    public async Task<ImportSessionResult> ImportAsync(ImportSessionRequest request)
    {
        CheckHeaders(request);
        var household = await user.GetCurrentHouseholdIdAsync();
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(request))));
        await using var unit = await context.Database.BeginTransactionAsync();
        var previous = await context.TransactionImportSessions.AsNoTracking().SingleOrDefaultAsync(s => s.HouseholdId == household && s.SubmissionId == request.SubmissionId);
        if (previous is not null) {
            if (previous.PayloadHash != hash) throw new ImportConflictException();
            var replay=JsonSerializer.Deserialize<ImportSessionResult>(previous.ResultJson)!;
            var replayIds=replay.Files.Select(f=>f.BatchId).ToArray();
            if (await context.TransactionImportBatches.AnyAsync(b=>replayIds.Contains(b.Id) && b.UndoneCount>0)) throw new ImportConflictException("This import was undone. Use a new reviewed session to import again.");
            return replay;
        }
        var submissions = request.Files.Select(f => f.SubmissionId).ToArray();
        if (await context.TransactionImportBatches.AnyAsync(b => b.HouseholdId == household && submissions.Contains(b.SubmissionId)))
            throw new ImportConflictException();
        // Validate all files and relationships again under the same transaction as the writes.
        foreach (var file in request.Files) {
            var check = await imports.ValidateAsync(file);
            if (!check.CanImport) throw new ImportSessionValidationException(new(false,[new(file.AccountId,check)],[],false));
        }
        await ValidateLinks(request,household);
        var results = new List<TransactionImportResponse>();
        foreach (var file in request.Files) results.Add(await imports.ImportAsync(file));
        foreach (var link in request.Links) {
            var first = await SavedEntry(link.First,household,results); var second = await SavedEntry(link.Second,household,results);
            await transfers.LinkAsync(new() { FirstTransactionId = first.Id, SecondTransactionId = second.Id,
                ExpectedFirstEditVersion = link.First.TransactionId.HasValue ? link.First.ExpectedEditVersion : first.EditVersion,
                ExpectedSecondEditVersion = link.Second.TransactionId.HasValue ? link.Second.ExpectedEditVersion : second.EditVersion });
        }
        await ImportBaseline.Capture(context,results.Select(f=>f.BatchId).ToArray());
        var result = new ImportSessionResult(request.SubmissionId,results,request.Links.Count);
        context.TransactionImportSessions.Add(new() { HouseholdId = household,SubmissionId=request.SubmissionId,PayloadHash=hash,ResultJson=JsonSerializer.Serialize(result),CreatedAt=clock.GetUtcNow().UtcDateTime });
        await context.SaveChangesAsync(); await unit.CommitAsync(); return result;
    }

    private async Task ValidateLinks(ImportSessionRequest request,int household)
    {
        var used = new HashSet<string>();
        foreach (var link in request.Links) {
            if (link is null || link.First is null || link.Second is null) throw new ArgumentException("Choose two transfer entries for each match.");
            if (link.First.TransactionId.HasValue && link.Second.TransactionId.HasValue) throw new ArgumentException("A match must include at least one row being imported.");
            var first = await ReviewedEntry(link.First,request,household); var second = await ReviewedEntry(link.Second,request,household);
            if (!used.Add(Key(link.First)) || !used.Add(Key(link.Second))) throw new ArgumentException("Each transfer entry can be matched only once.");
            if (first.AccountId == second.AccountId || first.Amount != second.Amount || first.TransferDirection == second.TransferDirection)
                throw new ArgumentException("Match equal amounts with opposite directions in different accounts.");
        }
    }
    private async Task<Transaction> ReviewedEntry(ImportSessionReference reference,ImportSessionRequest request,int household)
    {
        if (reference.TransactionId.HasValue == reference.RowId.HasValue) throw new ArgumentException("Choose either an existing transaction or a file row.");
        if (reference.TransactionId is int id) {
            var entry = await context.Transactions.AsNoTracking().SingleOrDefaultAsync(t => t.Id == id && t.HouseholdId == household && t.AccountId == reference.AccountId && t.Category.CategoryType == CategoryType.Transfer && t.TransferDirection != null);
            if (entry is null || entry.PairedTransactionId.HasValue || !reference.ExpectedEditVersion.HasValue || reference.ExpectedEditVersion != entry.EditVersion)
                throw new TransferConflictException("A matching transfer changed. Review transfer matches again.");
            return entry;
        }
        var row = request.Files.SingleOrDefault(f => f.AccountId == reference.AccountId)?.Rows.SingleOrDefault(r => r.RowId == reference.RowId);
        if (row is null || row.CategoryType != CategoryType.Transfer || row.Amount is null || row.Direction is null) throw new ArgumentException("The matched file row must be an included transfer.");
        return new() { AccountId=reference.AccountId,Amount=row.Amount.Value,TransferDirection=row.Direction };
    }
    private async Task<Transaction> SavedEntry(ImportSessionReference reference,int household,List<TransactionImportResponse> files)
    {
        if (reference.TransactionId is int id) return await context.Transactions.SingleAsync(t => t.Id==id && t.HouseholdId==household);
        var batch = files.Single(f => f.AccountId==reference.AccountId).BatchId;
        return await context.Transactions.SingleAsync(t => t.ImportBatchId==batch && t.ImportRowId==reference.RowId && t.HouseholdId==household);
    }
    private static string Key(ImportSessionReference r) => r.TransactionId is int id ? $"t:{id}" : $"r:{r.AccountId}:{r.RowId}";
    private static void CheckHeaders(ImportSessionRequest r)
    {
        if (r.SubmissionId==Guid.Empty || r.Files is null || r.Files.Count is <1 or >8 || r.Files.Any(f => f is null || f.Rows is null) || r.Files.Sum(f=>f.Rows.Count)>8000 || r.Links is null || r.Links.Count>4000)
            throw new ArgumentException("Choose up to 8 account files and 8,000 transactions per session.");
        if (r.Files.Select(f=>f.AccountId).Distinct().Count()!=r.Files.Count || r.Files.Select(f=>f.SubmissionId).Distinct().Count()!=r.Files.Count)
            throw new ArgumentException("Use one file per account, with a separate submission ID for each file.");
    }
}
public sealed class ImportSessionValidationException(ImportSessionValidation validation) : Exception("Review the changed account checks.") { public ImportSessionValidation Validation { get; } = validation; }
