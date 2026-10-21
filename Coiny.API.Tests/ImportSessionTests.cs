using Coiny.API.Data;
using Coiny.API.DTOs.Transactions;
using Coiny.API.Enums;
using Coiny.API.Interfaces;
using Coiny.API.Models;
using Coiny.API.Services;
using Microsoft.EntityFrameworkCore;
namespace Coiny.API.Tests;
public class ImportSessionTests
{
    private static readonly DateOnly Day = new(2026,10,1);
    private sealed class User : ICurrentUserService { public string? GetUserId()=>"test";public bool IsAuthenticated()=>true;public Task<int> GetCurrentHouseholdIdAsync()=>Task.FromResult(1); }
    private static ImportSessionService Service(CoinyContext c) {
        var user=new User();var snapshots=new AccountBalanceSnapshotService(c,user);
        return new(c,user,new TransactionImportService(c,user,snapshots,new AccountBalanceService(c),new TestClock()),new TransferService(c,user,snapshots),new TestClock());
    }
    private static async Task Seed(CoinyContext c) {
        c.Accounts.AddRange(new Account{Id=1,HouseholdId=1,Name="Bills",OpeningBalance=1000,OpeningBalanceDate=Day},new Account{Id=2,HouseholdId=1,Name="Spending",OpeningBalance=100,OpeningBalanceDate=Day},new Account{Id=3,HouseholdId=2,Name="Foreign",OpeningBalanceDate=Day});
        c.Categories.Add(new(){Id=1,HouseholdId=1,Name="Transfer",CategoryType=CategoryType.Transfer,IsActive=true,IsSystem=true});await c.SaveChangesAsync();
    }
    private static TransactionImportRequest File(int id,TransferDirection direction,decimal amount=250)=>new(){AccountId=id,SubmissionId=Guid.NewGuid(),Filename=$"account-{id}.csv",Rows=[new(){RowId=2,Date=Day.AddDays(id),Description=$"Transfer {id}",CategoryType=CategoryType.Transfer,Direction=direction,Amount=amount}]};
    private static ImportSessionRequest Session()=>new(){SubmissionId=Guid.NewGuid(),Files=[File(1,TransferDirection.Out),File(2,TransferDirection.In)]};
    private static ImportSessionLink Pair()=>new(new(1,2,null,null),new(2,2,null,null));
    [Fact] public async Task Multi_file_import_links_once_and_retry_replays_the_entire_session() {
        await using var db=await BudgetTestDatabase.CreateAsync();await Seed(db.Context());var request=Session();
        var check=await Service(db.Context()).ValidateAsync(request);Assert.True(check.CanImport);Assert.Single(check.Suggestions);
        request.Links.Add(Pair());var result=await Service(db.Context()).ImportAsync(request);Assert.Equal(1,result.LinkedPairs);Assert.Equal(2,result.Files.Count);
        using var verify=db.Context();Assert.Equal(2,await verify.Transactions.CountAsync());Assert.All(await verify.Transactions.ToListAsync(),t=>Assert.NotNull(t.PairedTransactionId));
        Assert.Equal(750,await new AccountBalanceService(verify).GetCurrentBalanceAsync(1));Assert.Equal(350,await new AccountBalanceService(verify).GetCurrentBalanceAsync(2));
        Assert.Equal(2,await verify.TransactionImportBatches.CountAsync());Assert.Single(await verify.TransactionImportSessions.ToListAsync());
        var replay=await Service(db.Context()).ImportAsync(request);Assert.Equal(result.SubmissionId,replay.SubmissionId);Assert.Equal(result.Files.Select(f=>f.BatchId),replay.Files.Select(f=>f.BatchId));Assert.Equal(2,await verify.Transactions.CountAsync());
        request.Links.Clear();await Assert.ThrowsAsync<ImportConflictException>(()=>Service(db.Context()).ImportAsync(request));
    }
    [Fact] public async Task New_rows_match_existing_transfers_without_reimporting_them_or_changing_their_balance() {
        await using var db=await BudgetTestDatabase.CreateAsync();await Seed(db.Context());var c=db.Context();
        var old=await new TransferService(c,new User(),new AccountBalanceSnapshotService(c,new User())).CreateEntryAsync(new(){AccountId=2,Amount=250,Date=Day.AddDays(2),Description="Already recorded",Direction=TransferDirection.In});
        var request=new ImportSessionRequest{SubmissionId=Guid.NewGuid(),Files=[File(1,TransferDirection.Out)]};
        var check=await Service(db.Context()).ValidateAsync(request);Assert.Single(check.Suggestions);Assert.Equal(old.Id,check.Suggestions[0].Second.Reference.TransactionId);
        request.Links.Add(new(new(1,2,null,null),new(2,null,old.Id,old.EditVersion)));
        await Service(db.Context()).ImportAsync(request);using var verify=db.Context();Assert.Equal(2,await verify.Transactions.CountAsync());Assert.Equal(350,await new AccountBalanceService(verify).GetCurrentBalanceAsync(2));
        Assert.Single(await verify.TransactionImportBatches.ToListAsync());Assert.Equal("Already recorded",(await verify.Transactions.FindAsync(old.Id))!.Description);
    }
    [Fact] public async Task Failure_in_second_account_rolls_back_first_account_batch_rows_and_snapshots() {
        await using var db=await BudgetTestDatabase.CreateAsync();await Seed(db.Context());var c=db.Context();
        await c.Database.ExecuteSqlRawAsync("CREATE TRIGGER reject_second_import BEFORE INSERT ON AccountBalanceSnapshots WHEN NEW.AccountId=2 BEGIN SELECT RAISE(ABORT,'failure'); END;");
        await Assert.ThrowsAsync<DbUpdateException>(()=>Service(db.Context()).ImportAsync(Session()));
        using var verify=db.Context();Assert.Empty(await verify.Transactions.ToListAsync());Assert.Empty(await verify.TransactionImportBatches.ToListAsync());Assert.Empty(await verify.AccountBalanceSnapshots.ToListAsync());Assert.Empty(await verify.TransactionImportSessions.ToListAsync());
    }
    [Fact] public async Task Duplicate_review_and_balance_acknowledgment_still_apply_per_account() {
        await using var db=await BudgetTestDatabase.CreateAsync();await Seed(db.Context());var initial=Session();await Service(db.Context()).ImportAsync(initial);
        var again=Session();var check=await Service(db.Context()).ValidateAsync(again);Assert.False(check.CanImport);Assert.All(check.Files,f=>Assert.False(f.Validation.CanImport));Assert.Empty(check.Suggestions);
        await Assert.ThrowsAsync<ImportSessionValidationException>(()=>Service(db.Context()).ImportAsync(again));
        var different=Session();different.Files[0].Rows[0].Amount=10;different.Files[1].Rows[0].Amount=10;
        different.Files[0].BankBalanceCheck=new(){Date=Day.AddDays(1),Balance=999};
        Assert.False((await Service(db.Context()).ValidateAsync(different)).CanImport);
        different.Files[0].BankBalanceCheck!.AcknowledgedCalculatedBalance=740;
        Assert.True((await Service(db.Context()).ValidateAsync(different)).CanImport);
    }
    [Fact] public async Task Stale_existing_match_rejects_whole_session_before_importing() {
        await using var db=await BudgetTestDatabase.CreateAsync();await Seed(db.Context());var c=db.Context();
        var old=await new TransferService(c,new User(),new AccountBalanceSnapshotService(c,new User())).CreateEntryAsync(new(){AccountId=2,Amount=250,Date=Day.AddDays(2),Description="Existing",Direction=TransferDirection.In});
        var request=new ImportSessionRequest{SubmissionId=Guid.NewGuid(),Files=[File(1,TransferDirection.Out)],Links=[new(new(1,2,null,null),new(2,null,old.Id,99))]};
        await Assert.ThrowsAsync<TransferConflictException>(()=>Service(db.Context()).ImportAsync(request));
        using var verify=db.Context();Assert.Single(await verify.Transactions.ToListAsync());Assert.Empty(await verify.TransactionImportBatches.ToListAsync());Assert.Empty(await verify.TransactionImportSessions.ToListAsync());
    }
    [Fact] public async Task One_to_one_matches_and_household_limits_are_enforced() {
        await using var db=await BudgetTestDatabase.CreateAsync();await Seed(db.Context());
        var request=Session();request.Links=[Pair(),Pair()];await Assert.ThrowsAsync<ArgumentException>(()=>Service(db.Context()).ImportAsync(request));
        request=Session();request.Files[1].AccountId=3;await Assert.ThrowsAsync<ArgumentException>(()=>Service(db.Context()).ValidateAsync(request));
        request=Session();request.Files[1].AccountId=1;await Assert.ThrowsAsync<ArgumentException>(()=>Service(db.Context()).ValidateAsync(request));
        request=Session();request.Links=[new(new(1,2,null,null),new(2,999,null,null))];await Assert.ThrowsAsync<ArgumentException>(()=>Service(db.Context()).ImportAsync(request));
        using var verify=db.Context();Assert.Empty(await verify.Transactions.ToListAsync());
    }
}
