using System.Text.Json;
using Coiny.API.Data;
using Microsoft.EntityFrameworkCore;
namespace Coiny.API.Services;
public record ImportOriginalState(int Id,int EditVersion,int? PairedTransactionId);
internal static class ImportBaseline
{
    public static async Task Capture(CoinyContext context,IReadOnlyCollection<int> ids)
    {
        var rows=await context.Transactions.AsNoTracking().Where(t=>t.ImportBatchId.HasValue && ids.Contains(t.ImportBatchId.Value))
            .Select(t=>new {BatchId=t.ImportBatchId!.Value,State=new ImportOriginalState(t.Id,t.EditVersion,t.PairedTransactionId)}).ToListAsync();
        foreach(var batch in await context.TransactionImportBatches.Where(b=>ids.Contains(b.Id)).ToListAsync())
            batch.OriginalStateJson=JsonSerializer.Serialize(rows.Where(r=>r.BatchId==batch.Id).Select(r=>r.State));
    }
}
