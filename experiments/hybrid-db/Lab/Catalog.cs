using Npgsql;
namespace HybridLab;

public class Catalog(NpgsqlDataSource source, LabOptions options)
{
    private const string Columns = "id,name,storage_key,status,owner_node,schema_version,pending_backup";
    private static Household Row(NpgsqlDataReader reader) => new(reader.GetGuid(0),reader.GetString(1),reader.GetGuid(2),reader.GetString(3),reader.GetString(4),reader.GetInt32(5),reader.IsDBNull(6)?null:reader.GetGuid(6));
    public async Task Initialize()
    {
        await using var command=source.CreateCommand("""
            CREATE TABLE IF NOT EXISTS lab_users(id uuid PRIMARY KEY,name text NOT NULL);
            CREATE TABLE IF NOT EXISTS households(id uuid PRIMARY KEY,name text NOT NULL,storage_key uuid NOT NULL UNIQUE,
                status text NOT NULL CHECK(status IN ('Provisioning','Ready','Maintenance')),owner_node text NOT NULL,
                schema_version int NOT NULL,pending_backup uuid NULL);
            CREATE TABLE IF NOT EXISTS memberships(household_id uuid REFERENCES households(id),user_id uuid REFERENCES lab_users(id),
                role text NOT NULL CHECK(role IN ('Owner','Member')),PRIMARY KEY(household_id,user_id));
            CREATE TABLE IF NOT EXISTS backups(id uuid PRIMARY KEY,household_id uuid NOT NULL REFERENCES households(id),
                sha256 text NOT NULL,created_at timestamptz NOT NULL DEFAULT now());
            CREATE TABLE IF NOT EXISTS control_schema(version int PRIMARY KEY);
            INSERT INTO control_schema VALUES(1) ON CONFLICT DO NOTHING;
            """);
        await command.ExecuteNonQueryAsync();
    }
    public async Task<Household?> Find(Guid id)
    {
        await using var command=source.CreateCommand($"SELECT {Columns} FROM households WHERE id=$1");command.Parameters.AddWithValue(id);
        await using var reader=await command.ExecuteReaderAsync();return await reader.ReadAsync()?Row(reader):null;
    }
    public async Task<Household> Authorize(Guid id,Guid user)
    {
        await using var command=source.CreateCommand($"SELECT {Columns} FROM households h WHERE id=$1 AND EXISTS(SELECT 1 FROM memberships m WHERE m.household_id=h.id AND m.user_id=$2)");
        command.Parameters.AddWithValue(id);command.Parameters.AddWithValue(user);
        await using var reader=await command.ExecuteReaderAsync();
        if(!await reader.ReadAsync())throw new LabFault(404,"Household not found.");
        var household=Row(reader);
        if(household.Status!="Ready"||household.Node!=options.Node||household.SchemaVersion<options.TargetSchema)
            throw new LabFault(503,"Household unavailable or requires maintenance.");
        return household;
    }
    public async Task<List<Household>> All()
    {
        await using var command=source.CreateCommand($"SELECT {Columns} FROM households ORDER BY id");
        await using var reader=await command.ExecuteReaderAsync();var rows=new List<Household>();while(await reader.ReadAsync())rows.Add(Row(reader));return rows;
    }
    public async Task<Household> Create(Guid user,string name,int version,Guid? id=null)
    {
        if(name.Length is <1 or >80||version is <1 or >2)throw new LabFault(400,"Invalid synthetic household.");
        var key=id??Guid.NewGuid();
        await using var connection=await source.OpenConnectionAsync();await using var transaction=await connection.BeginTransactionAsync();
        await using(var command=new NpgsqlCommand("INSERT INTO households VALUES($1,$2,$3,'Provisioning',$4,$5,NULL) ON CONFLICT(id) DO NOTHING",connection,transaction)){
            command.Parameters.AddWithValue(key);command.Parameters.AddWithValue(name);command.Parameters.AddWithValue(Guid.NewGuid());command.Parameters.AddWithValue(options.Node);command.Parameters.AddWithValue(version);await command.ExecuteNonQueryAsync();}
        await using(var command=new NpgsqlCommand("INSERT INTO memberships VALUES($1,$2,'Owner') ON CONFLICT DO NOTHING",connection,transaction)){
            command.Parameters.AddWithValue(key);command.Parameters.AddWithValue(user);await command.ExecuteNonQueryAsync();}
        await transaction.CommitAsync();return (await Find(key))!;
    }
    public async Task SeedUsers()
    {
        foreach(var (id,name) in Fixtures.Users){await using var command=source.CreateCommand("INSERT INTO lab_users VALUES($1,$2) ON CONFLICT DO NOTHING");command.Parameters.AddWithValue(id);command.Parameters.AddWithValue(name);await command.ExecuteNonQueryAsync();}
    }
    public async Task AddMember(Guid household,Guid user)
    {
        await using var command=source.CreateCommand("INSERT INTO memberships VALUES($1,$2,'Member') ON CONFLICT DO NOTHING");command.Parameters.AddWithValue(household);command.Parameters.AddWithValue(user);await command.ExecuteNonQueryAsync();
    }
    public async Task RemoveMember(Guid household,Guid user)
    {
        await using var command=source.CreateCommand("DELETE FROM memberships WHERE household_id=$1 AND user_id=$2");command.Parameters.AddWithValue(household);command.Parameters.AddWithValue(user);await command.ExecuteNonQueryAsync();
    }
    public async Task Ready(Guid id,int version)
    {
        await using var command=source.CreateCommand("UPDATE households SET status='Ready',schema_version=$2,pending_backup=NULL WHERE id=$1");command.Parameters.AddWithValue(id);command.Parameters.AddWithValue(version);await command.ExecuteNonQueryAsync();
    }
    public async Task Maintenance(Guid id,Guid? backup=null)
    {
        await using var command=source.CreateCommand("UPDATE households SET status='Maintenance',pending_backup=$2 WHERE id=$1");command.Parameters.AddWithValue(id);command.Parameters.Add(new NpgsqlParameter { NpgsqlDbType=NpgsqlTypes.NpgsqlDbType.Uuid,Value=backup is null?DBNull.Value:backup.Value });await command.ExecuteNonQueryAsync();
    }
    public async Task SetNode(Guid id,string node)
    {
        await using var command=source.CreateCommand("UPDATE households SET owner_node=$2 WHERE id=$1");command.Parameters.AddWithValue(id);command.Parameters.AddWithValue(node);await command.ExecuteNonQueryAsync();
    }
    public async Task SwapStorageForFaultTest(Guid a,Guid b)
    {
        var first=await Find(a)??throw new LabFault(404,"Household not found.");
        var second=await Find(b)??throw new LabFault(404,"Household not found.");
        if(a==b)throw new LabFault(400,"Choose distinct households.");
        await using var connection=await source.OpenConnectionAsync();await using var transaction=await connection.BeginTransactionAsync();
        foreach(var (id,key) in new[]{(a,Guid.NewGuid()),(b,first.StorageKey),(a,second.StorageKey)}){
            await using var command=new NpgsqlCommand("UPDATE households SET storage_key=$2 WHERE id=$1",connection,transaction);
            command.Parameters.AddWithValue(id);command.Parameters.AddWithValue(key);await command.ExecuteNonQueryAsync();
        }
        await transaction.CommitAsync();
    }
    public async Task<Guid> SaveBackup(Guid household,string hash)
    {
        var id=Guid.NewGuid();await SaveBackup(id,household,hash);return id;
    }
    public async Task SaveBackup(Guid id,Guid household,string hash)
    {
        await using var command=source.CreateCommand("INSERT INTO backups(id,household_id,sha256) VALUES($1,$2,$3)");command.Parameters.AddWithValue(id);command.Parameters.AddWithValue(household);command.Parameters.AddWithValue(hash);await command.ExecuteNonQueryAsync();
    }
    public async Task<string> BackupHash(Guid id,Guid household)
    {
        await using var command=source.CreateCommand("SELECT sha256 FROM backups WHERE id=$1 AND household_id=$2");command.Parameters.AddWithValue(id);command.Parameters.AddWithValue(household);
        return (string?)await command.ExecuteScalarAsync()??throw new LabFault(404,"Backup not found for this household.");
    }
}
public static class Fixtures
{
    public static readonly Guid Alice=Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa1"),Amy=Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa2"),Bob=Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbb1"),Eve=Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee");
    public static readonly Guid A=Guid.Parse("11111111-1111-1111-1111-111111111111"),B=Guid.Parse("22222222-2222-2222-2222-222222222222");
    public static readonly (Guid Id,string Name)[] Users=[(Alice,"Alice"),(Amy,"Amy"),(Bob,"Bob"),(Eve,"Eve")];
}
