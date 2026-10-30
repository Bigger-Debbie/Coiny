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

public class ImportUndoService(IFinancialContext context,ICurrentUserService user,IAccountBalanceSnapshotService snapshots,TimeProvider clock)
{
    private static string Hash(object value)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value))));
    private static ImportConflictException Changed()=>new("The import or affected accounts changed. Refresh the undo preview before confirming.");
    private static object State(Transaction t)=>new {t.Id,t.EditVersion,t.ImportBatchId,t.ImportRowId,t.AccountId,t.CategoryId,CategoryType=t.Category.CategoryType,t.Amount,t.TransactionDate,t.Description,t.Merchant,t.Notes,t.IsCleared,t.TransferDirection,t.TransferCounterparty,t.PairedTransactionId};
    private sealed record Review(ImportUndoPreview Preview,List<Transaction> Scope,List<Transaction> Pairs,List<TransactionImportBatch> Batches);
    public async Task<ImportUndoPreview> PreviewAsync(ImportUndoSelection request)=>(await Build(request)).Preview;
    public async Task<ImportUndoPreview> ReviewAsync(int batchId)=>await PreviewAsync(new(){BatchIds=[batchId]});
    private async Task<Review> Build(ImportUndoSelection request)
    {
        var household=await user.GetCurrentHouseholdIdAsync();
        if(request.BatchIds is null || request.BatchIds.Length is <1 or >8 || request.BatchIds.Distinct().Count()!=request.BatchIds.Length)
            throw new ArgumentException("Choose one imported file or the files in its session.");
        var batches=await context.TransactionImportBatches.AsNoTracking().Include(b=>b.Account).Where(b=>b.HouseholdId==household && request.BatchIds.Contains(b.Id)).OrderBy(b=>b.Id).ToListAsync();
        if(batches.Count!=request.BatchIds.Length) throw new ArgumentException("Choose imports from your household.");
        var relatedIds=request.BatchIds.ToHashSet();
        foreach(var batch in batches)
        {
            var token=$"\"BatchId\":{batch.Id}";
            var sessions=await context.TransactionImportSessions.AsNoTracking().Where(s=>s.HouseholdId==household && s.ResultJson.Contains(token)).Select(s=>s.ResultJson).ToListAsync();
            foreach(var json in sessions)
            {
                var session=JsonSerializer.Deserialize<ImportSessionResult>(json);
                if(session?.Files.Any(f=>f.BatchId==batch.Id)==true) relatedIds.UnionWith(session.Files.Select(f=>f.BatchId));
            }
        }
        if(request.BatchIds.Length>1 && relatedIds.Count>8) throw new ArgumentException("Review one import session at a time.");
        var related=await context.TransactionImportBatches.AsNoTracking().Include(b=>b.Account).Where(b=>b.HouseholdId==household && relatedIds.Contains(b.Id)).OrderBy(b=>b.Id).ToListAsync();
        var allCounts=await context.Transactions.AsNoTracking().Where(t=>t.HouseholdId==household && t.ImportBatchId.HasValue && relatedIds.Contains(t.ImportBatchId.Value)).GroupBy(t=>t.ImportBatchId!.Value).Select(g=>new{Id=g.Key,Count=g.Count()}).ToListAsync();
        var scope=await context.Transactions.AsNoTracking().Include(t=>t.Account).Include(t=>t.Category).Where(t=>t.HouseholdId==household && t.ImportBatchId.HasValue && request.BatchIds.Contains(t.ImportBatchId.Value)).OrderBy(t=>t.Id).ToListAsync();
        var pairIds=scope.Where(t=>t.PairedTransactionId.HasValue).Select(t=>t.PairedTransactionId!.Value).Distinct().ToArray();
        var pairs=await context.Transactions.AsNoTracking().Include(t=>t.Account).Include(t=>t.Category).Where(t=>t.HouseholdId==household && pairIds.Contains(t.Id)).OrderBy(t=>t.Id).ToListAsync();
        if(scope.Any(t=>t.PairedTransactionId is int id && !pairs.Any(p=>p.Id==id && p.PairedTransactionId==t.Id))) throw Changed();
        var movementIds=scope.Select(t=>t.Id).Concat(pairIds).Distinct().ToArray();
        var payments=await context.BillPayments.AsNoTracking().Include(p=>p.Links).Where(p=>p.HouseholdId==household && (p.TransactionId.HasValue && movementIds.Contains(p.TransactionId.Value) || p.Links.Any(l=>movementIds.Contains(l.TransactionId)))).OrderBy(p=>p.Id).ToListAsync();
        var savings=await context.Set<SavingsActivity>().AsNoTracking().Where(a=>a.SavingsFund.HouseholdId==household && a.TransactionId.HasValue && movementIds.Contains(a.TransactionId.Value)).OrderBy(a=>a.Id).ToListAsync();
        var assignments=await context.TransactionBudgetAssignments.AsNoTracking().Where(a=>a.HouseholdId==household && movementIds.Contains(a.TransactionId)).OrderBy(a=>a.Id).ToListAsync();
        var originals=batches.ToDictionary(b=>b.Id,b=>string.IsNullOrEmpty(b.OriginalStateJson)?new List<ImportOriginalState>():JsonSerializer.Deserialize<List<ImportOriginalState>>(b.OriginalStateJson) ?? []);
        var rows=scope.Select(t=>{
            var original=originals[t.ImportBatchId!.Value].FirstOrDefault(o=>o.Id==t.Id);
            var pair=pairs.FirstOrDefault(p=>p.Id==t.PairedTransactionId);
            bool Match(int? id)=>id==t.Id || pair is not null && id==pair.Id;
            return new ImportUndoRow(t.Id,t.ImportBatchId.Value,t.AccountId,t.Account.Name,DateOnly.FromDateTime(t.TransactionDate),t.Amount,t.Description,t.Merchant,
                original is not null && (original.EditVersion!=t.EditVersion || original.PairedTransactionId!=t.PairedTransactionId),original is null,
                t.PairedTransactionId,pair?.Account.Name,pair?.ImportBatchId,payments.Any(p=>Match(p.TransactionId)||p.Links.Any(l=>Match(l.TransactionId))) || savings.Any(a=>Match(a.TransactionId)),assignments.Any(a=>Match(a.TransactionId)));
        }).ToList();
        var remove=request.RemoveTransactionIds ?? rows.Where(r=>!r.ChangedSinceImport && !r.OriginalStateUnavailable).Select(r=>r.Id).ToArray();
        if(remove.Length>8000 || remove.Distinct().Count()!=remove.Length || remove.Any(id=>!scope.Any(t=>t.Id==id))) throw new ArgumentException("Choose transactions from the reviewed imports.");
        remove=remove.Order().ToArray();
        var selected=scope.Where(t=>remove.Contains(t.Id)).ToList();
        var affected=selected.GroupBy(t=>t.AccountId).ToDictionary(g=>g.Key,g=>g.Min(t=>DateOnly.FromDateTime(t.TransactionDate)));
        var accountIds=affected.Keys.ToArray();
        var accounts=await context.Accounts.AsNoTracking().Where(a=>a.HouseholdId==household && accountIds.Contains(a.Id)).OrderBy(a=>a.Id).ToListAsync();
        if(accounts.Any(a=>affected[a.Id]<a.OpeningBalanceDate)) throw new ArgumentException("An imported transaction predates its account's opening date. Correct that account before undoing this import.");
        var ledger=await context.Transactions.AsNoTracking().Include(t=>t.Category).Where(t=>t.HouseholdId==household && accountIds.Contains(t.AccountId)).OrderBy(t=>t.Id).ToListAsync();
        var impact=accounts.Select(a=>{
            var before=a.OpeningBalance+ledger.Where(t=>t.AccountId==a.Id).Sum(t=>AccountBalanceCalculator.GetBalanceChange(a.AccountType,t.Category.CategoryType,t.Amount,t.TransferDirection));
            var delta=selected.Where(t=>t.AccountId==a.Id).Sum(t=>AccountBalanceCalculator.GetBalanceChange(a.AccountType,t.Category.CategoryType,t.Amount,t.TransferDirection));
            return new ImportUndoAccount(a.Id,a.Name,before,before-delta);
        }).ToList();
        var checkpoints=(await context.AccountReconciliations.AsNoTracking().Where(r=>r.HouseholdId==household && accountIds.Contains(r.AccountId) && r.InvalidatedAt==null).OrderBy(r=>r.Id).ToListAsync())
            .Where(r=>r.StatementDate>=affected[r.AccountId]).Select(r=>new ImportUndoCheckpoint(r.Id,r.AccountId,accounts.Single(a=>a.Id==r.AccountId).Name,r.StatementDate,r.StatementBalance)).ToList();
        var history=(await context.Set<TransactionImportUndo>().AsNoTracking().Where(o=>o.HouseholdId==household).OrderByDescending(o=>o.Id).Select(o=>new{o.OperationId,o.ActorUserId,o.BatchIdsJson,o.ResultJson}).ToListAsync())
            .Where(o=>(JsonSerializer.Deserialize<int[]>(o.BatchIdsJson) ?? []).Any(id=>relatedIds.Contains(id))).Take(50)
            .Select(o=>JsonSerializer.Deserialize<ImportUndoResult>(o.ResultJson) is {} result ? new ImportUndoHistory(o.OperationId,result.CompletedAt,result.RemovedCount,o.ActorUserId):null).Where(o=>o is not null).Cast<ImportUndoHistory>().ToList();
        var fingerprint=Hash(new {Batches=batches.Select(b=>new {b.Id,b.OriginalStateJson,b.UndoneCount,b.LastUndoneAt}),Scope=scope.Select(State),Pairs=pairs.Select(State),Remove=remove,Accounts=accounts.Select(a=>new{a.Id,a.Name,a.AccountType,a.OpeningBalance,a.OpeningBalanceDate}),Ledger=ledger.Select(State),Payments=payments.Select(p=>new{p.Id,p.TransactionId,p.IsPaid,p.AllocationKey,p.DueDate,Links=p.Links.OrderBy(l=>l.Id).Select(l=>new{l.Id,l.TransactionId})}),Savings=savings.Select(a=>new{a.Id,a.Amount,a.Kind,a.TransactionId,a.ComponentKey,a.OperationId}),Assignments=assignments,Checkpoints=checkpoints});
        var detachedPairs=selected.Where(t=>t.PairedTransactionId.HasValue).Select(t=>Math.Min(t.Id,t.PairedTransactionId!.Value)).Distinct().Count();
        var preserved=selected.Where(t=>t.PairedTransactionId.HasValue && !remove.Contains(t.PairedTransactionId.Value)).Select(t=>t.PairedTransactionId).Distinct().Count();
        var preview=new ImportUndoPreview(request.BatchIds.Order().ToArray(),related.Select(b=>new ImportUndoFile(b.Id,b.Filename,b.AccountId,b.Account.Name,b.ImportedCount,allCounts.FirstOrDefault(g=>g.Id==b.Id)?.Count ?? 0,b.UndoneCount)).ToList(),rows,remove,impact,checkpoints,
            payments.Sum(p=>(p.TransactionId.HasValue && remove.Contains(p.TransactionId.Value)?1:0)+p.Links.Count(l=>remove.Contains(l.TransactionId)))+savings.Count(a=>remove.Contains(a.TransactionId!.Value)),assignments.Count(a=>remove.Contains(a.TransactionId)),detachedPairs,preserved,fingerprint,history);
        return new(preview,scope,pairs,batches);
    }
    public async Task<ImportUndoResult> UndoAsync(ImportUndoRequest request)
    {
        var household=await user.GetCurrentHouseholdIdAsync();var actor=user.GetUserId() ?? throw new UnauthorizedAccessException();
        if(request.OperationId==Guid.Empty || request.RemoveTransactionIds is null) throw new ArgumentException("Preview and explicitly select transactions before undoing.");
        var requestHash=Hash(request);
        await using var unit=await context.Database.BeginTransactionAsync();
        var replay=await context.Set<TransactionImportUndo>().AsNoTracking().SingleOrDefaultAsync(o=>o.HouseholdId==household && o.OperationId==request.OperationId);
        if(replay is not null) return replay.RequestHash==requestHash ? JsonSerializer.Deserialize<ImportUndoResult>(replay.ResultJson)! : throw Changed();
        var review=await Build(request);var p=review.Preview;
        if(p.Fingerprint!=request.ReviewedFingerprint) throw Changed();
        if(p.RemoveTransactionIds.Count==0) throw new ArgumentException("Choose at least one transaction to remove.");
        var changed=p.Rows.Any(r=>p.RemoveTransactionIds.Contains(r.Id) && (r.ChangedSinceImport || r.OriginalStateUnavailable));
        if(changed && !request.AcknowledgeChangedTransactions || p.PaymentLinksToDetach>0 && !request.AcknowledgePaymentLinks || p.Checkpoints.Count>0 && !request.AcknowledgeReconciliations)
            throw new ArgumentException("Acknowledge changed transactions, payment links, and affected reconciliation checkpoints before undoing.");
        var remove=p.RemoveTransactionIds.ToArray();var selected=review.Scope.Where(t=>remove.Contains(t.Id)).ToList();
        var now=clock.GetUtcNow().UtcDateTime;
        var checkpoints=await context.AccountReconciliations.Where(r=>p.Checkpoints.Select(c=>c.Id).Contains(r.Id)).ToListAsync();
        foreach(var checkpoint in checkpoints) {checkpoint.InvalidatedAt=now;checkpoint.InvalidationReason="Transactions in this verified period were removed by import undo.";}
        // Preserve the original row snapshots and effect preview as an audit record, not a restore promise.
        var detail=JsonSerializer.Serialize(new {Preview=p,RemovedTransactions=selected.Select(State)});
        var pairIds=selected.Where(t=>t.PairedTransactionId.HasValue).Select(t=>t.PairedTransactionId!.Value).Concat(remove).Distinct().ToArray();
        var tracked=await context.Transactions.Where(t=>t.HouseholdId==household && pairIds.Contains(t.Id)).ToListAsync();
        foreach(var t in tracked.Where(t=>t.PairedTransactionId.HasValue)) {t.PairedTransactionId=null;t.PairedTransaction=null;t.TransferCounterparty=TransferCounterparty.Unknown;t.EditVersion++;}
        try {
            await context.SaveChangesAsync();
            context.Transactions.RemoveRange(tracked.Where(t=>remove.Contains(t.Id)));
            await context.SaveChangesAsync();
            foreach(var account in p.Accounts) await snapshots.RecalculateFromAsync(account.AccountId,selected.Where(t=>t.AccountId==account.AccountId).Min(t=>DateOnly.FromDateTime(t.TransactionDate)));
            var batches=await context.TransactionImportBatches.Where(b=>request.BatchIds.Contains(b.Id) && b.HouseholdId==household).ToListAsync();
            foreach(var batch in batches.Where(b=>selected.Any(t=>t.ImportBatchId==b.Id))) {batch.UndoneCount+=selected.Count(t=>t.ImportBatchId==batch.Id);batch.LastUndoneAt=now;}
            var result=new ImportUndoResult(request.OperationId,DateTime.SpecifyKind(now,DateTimeKind.Utc),remove.Length,p.Accounts,p.Checkpoints.Count);
            context.Set<TransactionImportUndo>().Add(new(){HouseholdId=household,OperationId=request.OperationId,RequestHash=requestHash,ActorUserId=actor,BatchIdsJson=JsonSerializer.Serialize(request.BatchIds),DetailJson=detail,ResultJson=JsonSerializer.Serialize(result),CreatedAt=now});
            await context.SaveChangesAsync();await unit.CommitAsync();return result;
        } catch(DbUpdateConcurrencyException) {throw Changed();}
    }
}
