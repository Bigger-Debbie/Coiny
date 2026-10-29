using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Data.Sqlite;
namespace HybridLab;

public class HouseholdStorage(LabOptions options,Catalog catalog,Coordinator coordinator)
{
    private long fileOpenCount;
    public long FileOpenCount=>Interlocked.Read(ref fileOpenCount);
    private string FilePath(Household household)=>Path.Combine(options.DataRoot,"households",household.StorageKey.ToString("N")+".db");
    private string BackupPath(Guid id)=>Path.Combine(options.DataRoot,"backups",id.ToString("N")+".db");
    private async Task<SqliteConnection> Open(string path,SqliteOpenMode mode=SqliteOpenMode.ReadWrite)
    {
        Interlocked.Increment(ref fileOpenCount);
        var connection=new SqliteConnection(new SqliteConnectionStringBuilder { DataSource=path,Mode=mode,Pooling=false,DefaultTimeout=5 }.ToString());
        await connection.OpenAsync();await Execute(connection,"PRAGMA foreign_keys=ON;");return connection;
    }
    private static async Task Execute(SqliteConnection connection,string sql,SqliteTransaction? transaction=null)
    {await using var command=connection.CreateCommand();command.Transaction=transaction;command.CommandText=sql;await command.ExecuteNonQueryAsync();}
    private static async Task<(int Version,string Instance)> Identity(SqliteConnection connection,Guid household)
    {
        await using var command=connection.CreateCommand();command.CommandText="SELECT household_id,version,instance_id FROM metadata";
        await using var reader=await command.ExecuteReaderAsync();
        if(!await reader.ReadAsync()||reader.GetString(0)!=household.ToString())throw new LabFault(503,"Household file identity mismatch.");
        return(reader.GetInt32(1),reader.GetString(2));
    }
    private void Own(Household household){if(household.Node!=options.Node)throw new LabFault(503,"Wrong household node.");}
    public async Task Provision(Household household,string? failAt=null)
    {
        Own(household);if(household.Status=="Ready")return;
        using var maintenance=await coordinator.Maintain(household.Id);
        if(failAt=="after-catalog")return;
        var path=FilePath(household);
        await using(var connection=await Open(path,SqliteOpenMode.ReadWriteCreate)){
            await Execute(connection,"PRAGMA journal_mode=WAL;");
            await using var transaction=connection.BeginTransaction();
            await Execute(connection,"""
                CREATE TABLE IF NOT EXISTS metadata(household_id TEXT NOT NULL,version INTEGER NOT NULL,instance_id TEXT NOT NULL);
                CREATE TABLE IF NOT EXISTS imports(operation_id TEXT PRIMARY KEY,body_hash TEXT NOT NULL);
                CREATE TABLE IF NOT EXISTS transactions(id INTEGER PRIMARY KEY,operation_id TEXT NOT NULL REFERENCES imports(operation_id),description TEXT NOT NULL,amount_cents INTEGER NOT NULL);
                CREATE TABLE IF NOT EXISTS write_probe(id INTEGER PRIMARY KEY,ticks INTEGER NOT NULL);
                INSERT OR IGNORE INTO write_probe VALUES(1,0);
                """,transaction);
            await using(var command=connection.CreateCommand()){
                command.Transaction=transaction;command.CommandText="INSERT INTO metadata SELECT $id,1,$instance WHERE NOT EXISTS(SELECT 1 FROM metadata)";
                command.Parameters.AddWithValue("$id",household.Id.ToString());command.Parameters.AddWithValue("$instance",Guid.NewGuid().ToString());await command.ExecuteNonQueryAsync();}
            await transaction.CommitAsync();await Identity(connection,household.Id);
            await Upgrade(connection,household.Id,household.SchemaVersion);
        }
        if(failAt=="after-file")return;
        await catalog.Ready(household.Id,household.SchemaVersion);
    }
    private static async Task<int> Upgrade(SqliteConnection connection,Guid id,int target)
    {
        var identity=await Identity(connection,id);
        if(identity.Version>target)throw new LabFault(503,"File schema is newer than this app.");
        if(identity.Version==1&&target==2){
            await using var transaction=connection.BeginTransaction();
            await Execute(connection,"ALTER TABLE transactions ADD COLUMN memo TEXT NOT NULL DEFAULT ''; UPDATE metadata SET version=2;",transaction);
            await transaction.CommitAsync();
        }
        return(await Identity(connection,id)).Version;
    }
    public async Task Migrate(Household household,bool failAfterCommit=false)
    {
        Own(household);using var maintenance=await coordinator.Maintain(household.Id);
        await catalog.Maintenance(household.Id);
        await using var connection=await Open(FilePath(household));var version=await Upgrade(connection,household.Id,options.TargetSchema);
        if(!failAfterCommit)await catalog.Ready(household.Id,version);
    }
    public async Task Recover()
    {
        foreach(var household in await catalog.All()){
            if(household.Node!=options.Node)continue;
            if(household.Status=="Provisioning")await Provision(household);
            else if(household.Status=="Maintenance"){
                if(household.PendingBackup is {} backup)await Restore(household,backup);
                else await Migrate(household);
            }
            else if(household.SchemaVersion<options.TargetSchema)await Migrate(household);
        }
    }
    public async Task<object> Snapshot(Household household)
    {
        using var operation=coordinator.Enter(household.Id);await using var connection=await Open(FilePath(household));
        var identity=await Identity(connection,household.Id);
        await using var command=connection.CreateCommand();command.CommandText="SELECT COUNT(*),COALESCE(SUM(amount_cents),0) FROM transactions";
        await using var reader=await command.ExecuteReaderAsync();await reader.ReadAsync();
        return new {HouseholdId=household.Id,InstanceId=identity.Instance,SchemaVersion=identity.Version,Rows=reader.GetInt64(0),BalanceCents=reader.GetInt64(1)};
    }
    public async Task<object> Import(Household household,ImportRequest request)
    {
        if(request.OperationId==Guid.Empty||request.Rows is null||request.Rows.Length is <1 or >2000||request.Rows.Any(row=>row.Description is null||row.Description.Length>200||row.AmountCents is <-1000000000000 or >1000000000000))throw new LabFault(400,"Invalid synthetic import.");
        var hash=Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(request.Rows)));
        using var operation=coordinator.Enter(household.Id);var writer=coordinator.Get(household.Id).Writer;await writer.WaitAsync();
        try{
            await using var connection=await Open(FilePath(household));await Identity(connection,household.Id);await using var transaction=connection.BeginTransaction();
            await using(var existing=connection.CreateCommand()){
                existing.Transaction=transaction;existing.CommandText="SELECT body_hash FROM imports WHERE operation_id=$id";existing.Parameters.AddWithValue("$id",request.OperationId.ToString());
                if(await existing.ExecuteScalarAsync() is string previous){if(previous!=hash)throw new LabFault(409,"Operation ID reused with different input.");return new {Replayed=true,RowsAdded=0};}}
            await using(var command=connection.CreateCommand()){
                command.Transaction=transaction;command.CommandText="INSERT INTO imports VALUES($id,$hash)";command.Parameters.AddWithValue("$id",request.OperationId.ToString());command.Parameters.AddWithValue("$hash",hash);await command.ExecuteNonQueryAsync();}
            await using(var command=connection.CreateCommand()){
                command.Transaction=transaction;command.CommandText="INSERT INTO transactions(operation_id,description,amount_cents) VALUES($id,$description,$amount)";
                command.Parameters.AddWithValue("$id",request.OperationId.ToString());var description=command.Parameters.Add("$description",SqliteType.Text);var amount=command.Parameters.Add("$amount",SqliteType.Integer);command.Prepare();
                foreach(var row in request.Rows){description.Value=row.Description;amount.Value=row.AmountCents;await command.ExecuteNonQueryAsync();}}
            await transaction.CommitAsync();return new {Replayed=false,RowsAdded=request.Rows.Length};
        }finally{writer.Release();}
    }
    public async Task HoldWriter(Household household,int milliseconds)
    {
        if(milliseconds is <100 or >5000)throw new LabFault(400,"Hold must be 100–5000ms.");
        using var operation=coordinator.Enter(household.Id);var writer=coordinator.Get(household.Id).Writer;await writer.WaitAsync();
        try{
            await using var connection=await Open(FilePath(household));await using var transaction=connection.BeginTransaction();
            await Execute(connection,"UPDATE write_probe SET ticks=ticks+1",transaction);coordinator.Holds[household.Id]=true;
            await Task.Delay(milliseconds);await transaction.CommitAsync();
        }finally{coordinator.Holds[household.Id]=false;writer.Release();}
    }
    public async Task<object> Backup(Household household)
    {
        Own(household);using var maintenance=await coordinator.Maintain(household.Id);var id=Guid.NewGuid();
        await using(var source=await Open(FilePath(household))){await Identity(source,household.Id);await using var destination=await Open(BackupPath(id),SqliteOpenMode.ReadWriteCreate);source.BackupDatabase(destination);}
        var hash=Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(BackupPath(id))));await catalog.SaveBackup(id,household.Id,hash);
        return new {BackupId=id,HouseholdId=household.Id};
    }
    public async Task Restore(Household household,Guid backup,bool failAfterMarker=false)
    {
        Own(household);var expected=await catalog.BackupHash(backup,household.Id);var path=BackupPath(backup);
        var actual=Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(path)));
        if(expected!=actual)throw new LabFault(409,"Backup checksum mismatch.");
        using var maintenance=await coordinator.Maintain(household.Id);
        await using(var check=await Open(path,SqliteOpenMode.ReadOnly)){
            await Identity(check,household.Id);await using var command=check.CreateCommand();command.CommandText="PRAGMA integrity_check";
            if((string?)await command.ExecuteScalarAsync()!="ok")throw new LabFault(409,"Backup integrity check failed.");}
        await catalog.Maintenance(household.Id,backup);if(failAfterMarker)return;
        await using(var source=await Open(path,SqliteOpenMode.ReadOnly)){
            await using var destination=await Open(FilePath(household));source.BackupDatabase(destination);}
        await using(var restored=await Open(FilePath(household))){await Execute(restored,"PRAGMA journal_mode=WAL;");var version=await Upgrade(restored,household.Id,options.TargetSchema);await catalog.Ready(household.Id,version);}
    }
    public async Task<object> Inventory()
    {
        var result=new List<object>();
        foreach(var household in await catalog.All()){
            var path=FilePath(household);var exists=File.Exists(path);
            result.Add(new {household.Id,household.Name,household.Status,household.Node,household.SchemaVersion,household.PendingBackup,FileExists=exists,StorageKey=household.StorageKey});
        }
        return result;
    }
}
