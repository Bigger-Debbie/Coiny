using Coiny.API.Data;
using Coiny.API.DTOs.Transactions;
using Coiny.API.DTOs.Trends;
using Coiny.API.Enums;
using Coiny.API.Interfaces;
using Coiny.API.Models;
using Coiny.API.Services;
using Microsoft.EntityFrameworkCore;
namespace Coiny.API.Tests;
public class ImportUndoTests
{
    private static readonly DateOnly Day=new(2026,10,1);
    private sealed class User(int household=1):ICurrentUserService {public string? GetUserId()=>"tester";public bool IsAuthenticated()=>true;public Task<int> GetCurrentHouseholdIdAsync()=>Task.FromResult(household);}
    private static TransactionImportService Import(CoinyContext c)=>new(c,new User(),new AccountBalanceSnapshotService(c,new User()),new AccountBalanceService(c),new TestClock());
    private static ImportUndoService Undo(BudgetTestDatabase db,int household=1,IAccountBalanceSnapshotService? snapshots=null) {var c=db.Context();return new(c,new User(household),snapshots ?? new AccountBalanceSnapshotService(c,new User(household)),db.Clock);}
    private static ImportUndoRequest Confirm(ImportUndoPreview p)=>new(){BatchIds=p.BatchIds.ToArray(),RemoveTransactionIds=p.RemoveTransactionIds.ToArray(),ReviewedFingerprint=p.Fingerprint,OperationId=Guid.NewGuid(),AcknowledgeChangedTransactions=true,AcknowledgePaymentLinks=true,AcknowledgeReconciliations=true};
    private static async Task Seed(BudgetTestDatabase db)
    {
        var c=db.Context();c.Accounts.AddRange(new Account{Id=1,HouseholdId=1,Name="Bills",OpeningBalance=1000,OpeningBalanceDate=Day},new Account{Id=2,HouseholdId=1,Name="Spending",OpeningBalance=100,OpeningBalanceDate=Day},new Account{Id=3,HouseholdId=2,Name="Foreign",OpeningBalanceDate=Day});
        c.Categories.AddRange(new Category{Id=1,HouseholdId=1,Name="Expense",CategoryType=CategoryType.Expense,IsActive=true},new Category{Id=2,HouseholdId=1,Name="Transfer",CategoryType=CategoryType.Transfer,IsActive=true});
        c.AccountBalanceSnapshots.Add(new(){AccountId=1,SnapshotDate=Day.AddDays(10),Balance=1000});await c.SaveChangesAsync();
    }
    private static TransactionImportRequest File(int account=1,bool transfer=false,TransferDirection direction=TransferDirection.Out)=>new(){AccountId=account,SubmissionId=Guid.NewGuid(),Filename=$"account-{account}.csv",Rows=[new(){RowId=2,Date=Day.AddDays(1),Amount=20,CategoryType=transfer?CategoryType.Transfer:CategoryType.Expense,CategoryId=transfer?null:1,Direction=direction,Description="Imported row",IsCleared=true}]};
    [Fact] public async Task Undo_recalculates_snapshots_retains_audit_and_retries_idempotently_without_reimporting()
    {
        await using var db=await BudgetTestDatabase.CreateAsync();await Seed(db);var request=File();var batch=await Import(db.Context()).ImportAsync(request);
        var p=await Undo(db).ReviewAsync(batch.BatchId);Assert.Single(p.RemoveTransactionIds);Assert.False(p.Rows[0].OriginalStateUnavailable);Assert.Equal(980,p.Accounts[0].Before);Assert.Equal(1000,p.Accounts[0].After);
        var confirmation=Confirm(p);var result=await Undo(db).UndoAsync(confirmation);Assert.Equal(1,result.RemovedCount);
        var retry=await Undo(db).UndoAsync(confirmation);Assert.Equal(result.OperationId,retry.OperationId);Assert.Equal(result.RemovedCount,retry.RemovedCount);Assert.Equal(result.Accounts.ToArray(),retry.Accounts.ToArray());
        Assert.Empty(await db.Context().Transactions.ToListAsync());Assert.All(await db.Context().AccountBalanceSnapshots.ToListAsync(),s=>Assert.Equal(1000,s.Balance));
        Assert.Single(await db.Context().Set<TransactionImportUndo>().ToListAsync());Assert.Equal(1,(await db.Context().TransactionImportBatches.SingleAsync()).UndoneCount);
        var history=await Undo(db).ReviewAsync(batch.BatchId);Assert.Empty(history.Rows);Assert.Single(history.UndoHistory);
        await Assert.ThrowsAsync<ImportConflictException>(()=>Import(db.Context()).ImportAsync(request));
    }
    [Fact] public async Task Edited_and_legacy_entries_require_explicit_selection_and_acknowledgment()
    {
        await using var db=await BudgetTestDatabase.CreateAsync();await Seed(db);var batch=await Import(db.Context()).ImportAsync(File());
        var c=db.Context();var t=await c.Transactions.SingleAsync();t.Description="Corrected";t.EditVersion++;await c.SaveChangesAsync();
        var p=await Undo(db).ReviewAsync(batch.BatchId);Assert.True(p.Rows[0].ChangedSinceImport);Assert.Empty(p.RemoveTransactionIds);
        (await c.TransactionImportBatches.SingleAsync()).OriginalStateJson="";await c.SaveChangesAsync();
        p=await Undo(db).PreviewAsync(new(){BatchIds=[batch.BatchId],RemoveTransactionIds=[t.Id]});Assert.True(p.Rows[0].OriginalStateUnavailable);
        var request=Confirm(p);request.AcknowledgeChangedTransactions=false;await Assert.ThrowsAsync<ArgumentException>(()=>Undo(db).UndoAsync(request));
        Assert.Single(await db.Context().Transactions.ToListAsync());request.AcknowledgeChangedTransactions=true;await Undo(db).UndoAsync(request);
    }
    [Fact] public async Task Stale_balance_previews_and_foreign_imports_cannot_be_undone()
    {
        await using var db=await BudgetTestDatabase.CreateAsync();await Seed(db);var batch=await Import(db.Context()).ImportAsync(File());var p=await Undo(db).ReviewAsync(batch.BatchId);
        var c=db.Context();c.Transactions.Add(new(){HouseholdId=1,AccountId=1,CategoryId=1,Amount=5,Description="New expense",TransactionDate=Day.AddDays(2).ToDateTime(TimeOnly.MinValue)});await c.SaveChangesAsync();
        await Assert.ThrowsAsync<ImportConflictException>(()=>Undo(db).UndoAsync(Confirm(p)));Assert.Equal(2,await c.Transactions.CountAsync());
        var fresh=await Undo(db).ReviewAsync(batch.BatchId);(await c.Categories.SingleAsync(category=>category.Id==1)).CategoryType=CategoryType.Income;await c.SaveChangesAsync();
        await Assert.ThrowsAsync<ImportConflictException>(()=>Undo(db).UndoAsync(Confirm(fresh)));
        await Assert.ThrowsAsync<ArgumentException>(()=>Undo(db,2).ReviewAsync(batch.BatchId));
        await Assert.ThrowsAsync<ArgumentException>(()=>Undo(db).PreviewAsync(new(){BatchIds=[batch.BatchId],RemoveTransactionIds=[999]}));
    }
    [Fact] public async Task Reconciliation_records_are_invalidated_only_when_removed_dates_affect_them_and_payment_state_survives()
    {
        await using var db=await BudgetTestDatabase.CreateAsync();await Seed(db);var batch=await Import(db.Context()).ImportAsync(File());var c=db.Context();var t=await c.Transactions.SingleAsync();
        c.Transactions.Add(new(){Id=100,HouseholdId=1,AccountId=1,CategoryId=1,Amount=5,Description="Other payment",TransactionDate=Day.AddDays(2).ToDateTime(TimeOnly.MinValue)});await c.SaveChangesAsync();
        c.BillPayments.Add(new(){HouseholdId=1,AllocationKey=Guid.NewGuid(),DueDate=Day.AddDays(1),IsPaid=true,TransactionId=t.Id,Links=[new(){TransactionId=100}]});
        c.AccountReconciliations.AddRange(new AccountReconciliation{HouseholdId=1,AccountId=1,StatementDate=Day,StatementBalance=1000},new AccountReconciliation{HouseholdId=1,AccountId=1,StatementDate=Day.AddDays(5),StatementBalance=975});
        c.TransactionBudgetAssignments.Add(new(){HouseholdId=1,TransactionId=t.Id,AllocationKey=null});await c.SaveChangesAsync();
        var p=await Undo(db).ReviewAsync(batch.BatchId);Assert.Single(p.Checkpoints);Assert.Equal(1,p.PaymentLinksToDetach);Assert.Equal(1,p.BudgetAssignmentsToRemove);
        var request=Confirm(p);request.AcknowledgeReconciliations=false;await Assert.ThrowsAsync<ArgumentException>(()=>Undo(db).UndoAsync(request));request.AcknowledgeReconciliations=true;
        await Undo(db).UndoAsync(request);
        var checkpoints=await db.Context().AccountReconciliations.OrderBy(r=>r.StatementDate).ToListAsync();Assert.Null(checkpoints[0].InvalidatedAt);Assert.NotNull(checkpoints[1].InvalidatedAt);Assert.Contains("import undo",checkpoints[1].InvalidationReason);
        var paid=await db.Context().BillPayments.Include(p=>p.Links).SingleAsync();Assert.True(paid.IsPaid);Assert.Null(paid.TransactionId);Assert.Equal(100,Assert.Single(paid.Links).TransactionId);Assert.Empty(await db.Context().TransactionBudgetAssignments.ToListAsync());
    }
    [Theory][InlineData(false)][InlineData(true)]
    public async Task Session_undo_can_remove_one_file_or_both_sides_without_deleting_unselected_counterparts(bool whole)
    {
        await using var db=await BudgetTestDatabase.CreateAsync();await Seed(db);var c=db.Context();var snapshots=new AccountBalanceSnapshotService(c,new User());
        var sessions=new ImportSessionService(c,new User(),Import(c),new TransferService(c,new User(),snapshots),db.Clock);
        var request=new ImportSessionRequest{SubmissionId=Guid.NewGuid(),Files=[File(1,true),File(2,true,TransferDirection.In)],Links=[new(new(1,2,null,null),new(2,2,null,null))]};
        var imported=await sessions.ImportAsync(request);var first=imported.Files[0].BatchId;var review=await Undo(db).ReviewAsync(first);Assert.Equal(2,review.RelatedFiles.Count);Assert.False(review.Rows[0].ChangedSinceImport);
        var ids=whole?imported.Files.Select(f=>f.BatchId).ToArray():new[]{first};var preview=await Undo(db).PreviewAsync(new(){BatchIds=ids});
        Assert.Equal(whole?0:1,preview.CounterpartsPreserved);await Undo(db).UndoAsync(Confirm(preview));
        var remaining=await db.Context().Transactions.ToListAsync();Assert.Equal(whole?0:1,remaining.Count);
        if(!whole){Assert.Null(remaining[0].PairedTransactionId);Assert.Equal(TransferCounterparty.Unknown,remaining[0].TransferCounterparty);Assert.Equal(120,await new AccountBalanceService(db.Context()).GetCurrentBalanceAsync(2));}
        await Assert.ThrowsAsync<ImportConflictException>(()=>sessions.ImportAsync(request));
    }
    [Fact] public async Task Matching_an_import_to_a_preexisting_transfer_preserves_that_transfer_when_undoing()
    {
        await using var db=await BudgetTestDatabase.CreateAsync();await Seed(db);var c=db.Context();var transfers=new TransferService(c,new User(),new AccountBalanceSnapshotService(c,new User()));
        var existing=await transfers.CreateEntryAsync(new(){AccountId=2,Date=Day.AddDays(1),Amount=20,Description="Existing incoming",Direction=TransferDirection.In});
        var session=new ImportSessionService(c,new User(),Import(c),transfers,db.Clock);
        var imported=await session.ImportAsync(new(){SubmissionId=Guid.NewGuid(),Files=[File(1,true)],Links=[new(new(1,2,null,null),new(2,null,existing.Id,existing.EditVersion))]});
        var preview=await Undo(db).ReviewAsync(imported.Files[0].BatchId);Assert.Equal(1,preview.CounterpartsPreserved);await Undo(db).UndoAsync(Confirm(preview));
        var preserved=Assert.Single(await db.Context().Transactions.ToListAsync());Assert.Equal(existing.Id,preserved.Id);Assert.Null(preserved.PairedTransactionId);Assert.Equal(20,preserved.Amount);
    }
    private sealed class FailingSnapshots:IAccountBalanceSnapshotService {public Task RecalculateFromAsync(int id,DateOnly date)=>throw new InvalidOperationException("Snapshot failure");public Task<IReadOnlyList<TrendDataPoint>> GetHistoryAsync(int id,DateOnly start,DateOnly end)=>throw new NotImplementedException();}
    [Fact] public async Task Failure_rolls_back_ledger_links_checkpoints_batch_metadata_and_audit()
    {
        await using var db=await BudgetTestDatabase.CreateAsync();await Seed(db);var batch=await Import(db.Context()).ImportAsync(File());var c=db.Context();c.AccountReconciliations.Add(new(){HouseholdId=1,AccountId=1,StatementDate=Day.AddDays(3),StatementBalance=980});await c.SaveChangesAsync();
        var p=await Undo(db).ReviewAsync(batch.BatchId);await Assert.ThrowsAsync<InvalidOperationException>(()=>Undo(db,snapshots:new FailingSnapshots()).UndoAsync(Confirm(p)));
        Assert.Single(await db.Context().Transactions.ToListAsync());Assert.Null((await db.Context().AccountReconciliations.SingleAsync()).InvalidatedAt);Assert.Equal(0,(await db.Context().TransactionImportBatches.SingleAsync()).UndoneCount);Assert.Empty(await db.Context().Set<TransactionImportUndo>().ToListAsync());
    }
}
