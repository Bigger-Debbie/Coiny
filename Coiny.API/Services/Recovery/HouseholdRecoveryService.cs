using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Coiny.API.Data;
using Coiny.API.Data.Hybrid;
using Coiny.API.Enums;
using Coiny.API.Interfaces;
using Coiny.API.Models;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
namespace Coiny.API.Services.Recovery;

public sealed record CreateBackupRequest(string CurrentPassword, bool PasswordProtected, string? BackupPassword);
public sealed record RestoreRequest(Guid PreviewId, string CurrentPassword, bool ConfirmReplacement);
internal sealed record HouseholdPayload(int Version, int Schema, int HouseholdId, Guid StorageKey, DateTime CreatedAt, string Sha256, byte[] Database);
internal sealed record RestoreTicket(string UserId, int HouseholdId, Guid TargetKey, string TargetHash, DateTime ExpiresAt, bool Portable, HouseholdPayload Payload);
public sealed record RecoveryPreview(Guid PreviewId, DateTime ExpiresAt, DateTime BackupCreatedAt, bool Portable, int BackupAccounts, int BackupTransactions, int CurrentAccounts, int CurrentTransactions);

// Controller's HouseholdStorage middleware owns the household writer gate for all
// POSTs. Restore publishes a fresh file + atomic registry switch, never overwrites
// active SQLite files. Existing readers may finish; later requests open the new file.
public sealed class HouseholdRecoveryService(ControlContext control, HouseholdRequestContext request,
    HybridStorageRoot root, ICurrentUserService current, UserManager<ApplicationUser> users,
    BackupEncryption encryption, IDataProtectionProvider protection, TimeProvider clock)
{
    private HouseholdContext Finance => request.Context ?? throw new HouseholdStorageUnavailableException();
    private DateTime Now => clock.GetUtcNow().UtcDateTime;
    private int Household;
    private string UserId = "";
    private string DirectoryFor(string kind) {
        var path = Path.Combine(root.DirectoryPath, ".recovery", Household.ToString(CultureInfo.InvariantCulture), kind);
        Directory.CreateDirectory(path);
        if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        return path;
    }
    internal static async Task WritePrivateAsync(string path, byte[] bytes)
    {
        var options = new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write, Share = FileShare.None };
        if (!OperatingSystem.IsWindows()) options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        await using var stream = new FileStream(path, options); await stream.WriteAsync(bytes); stream.Flush(flushToDisk: true);
    }
    private async Task AuthorizeAsync(string? password = null)
    {
        Household = await current.GetCurrentHouseholdIdAsync(); UserId = current.GetUserId() ?? throw new UnauthorizedAccessException();
        if (!await control.HouseholdMembers.AnyAsync(x => x.HouseholdId == Household && x.UserId == UserId && x.Role == HouseholdRole.Owner))
            throw new UnauthorizedAccessException("Only a household owner can back up, export or restore household data.");
        if (password != null) {
            var user = await users.FindByIdAsync(UserId);
            if (user == null || !await users.CheckPasswordAsync(user, password)) throw new ArgumentException("Your current account password is incorrect.");
        }
    }
    private async Task<HouseholdDatabase> RegistryAsync() => await control.HouseholdDatabases.AsNoTracking().SingleAsync(x => x.HouseholdId == Household);
    internal static string Hash(byte[] value) => Convert.ToHexString(SHA256.HashData(value));
    private async Task<HouseholdPayload> SnapshotAsync()
    {
        var directory = DirectoryFor("staging"); var path = Path.Combine(directory, Guid.NewGuid().ToString("N") + ".sqlite");
        try {
            await WritePrivateAsync(path, []);
            await using (var target = Connection(path, SqliteOpenMode.ReadWrite)) {
                await target.OpenAsync(); ((SqliteConnection)Finance.Database.GetDbConnection()).BackupDatabase(target);
            }
            if (new FileInfo(path).Length > 32 * 1024 * 1024) throw new ArgumentException("This household is too large for a downloaded backup.");
            var bytes = await File.ReadAllBytesAsync(path);
            var metadata = await Finance.Set<HouseholdDatabaseMetadata>().AsNoTracking().SingleAsync();
            return new(1, HouseholdDatabaseService.CurrentSchema, Household, metadata.StorageKey, Now, Hash(bytes), bytes);
        } finally { if (File.Exists(path)) File.Delete(path); }
    }
    public async Task<byte[]> CreateAsync(CreateBackupRequest input)
    {
        await AuthorizeAsync(input.CurrentPassword ?? "");
        var payload = await SnapshotAsync();
        return encryption.Encrypt(JsonSerializer.SerializeToUtf8Bytes(payload), input.PasswordProtected ? input.BackupPassword ?? "" : null);
    }
    private static SqliteConnection Connection(string path, SqliteOpenMode mode) => new(new SqliteConnectionStringBuilder { DataSource = path, Mode = mode, Pooling = false, ForeignKeys = true, DefaultTimeout = 5 }.ToString());
    private static string Quote(string name) => "\"" + name.Replace("\"", "\"\"") + "\"";
    private static async Task<List<string>> SchemaAsync(SqliteConnection connection)
    {
        await using var command = connection.CreateCommand(); command.CommandText = "SELECT type || ':' || name || ':' || COALESCE(sql,'') FROM sqlite_master WHERE name NOT LIKE 'sqlite_%' ORDER BY type,name";
        var result = new List<string>(); await using var reader = await command.ExecuteReaderAsync(); while (await reader.ReadAsync()) result.Add(reader.GetString(0)); return result;
    }
    private async Task<(string Path, int Accounts, int Transactions)> ValidateAsync(HouseholdPayload payload)
    {
        if (payload.Version != 1 || payload.Schema != HouseholdDatabaseService.CurrentSchema || payload.HouseholdId <= 0 || payload.StorageKey == Guid.Empty || payload.Database == null || payload.Database.Length > 32 * 1024 * 1024 || Hash(payload.Database) != payload.Sha256)
            throw new ArgumentException("The backup is damaged or its version is incompatible.");
        var path = Path.Combine(DirectoryFor("staging"), Guid.NewGuid().ToString("N") + ".sqlite");
        try {
            await WritePrivateAsync(path, payload.Database);
            await using var source = Connection(path, SqliteOpenMode.ReadOnly); await source.OpenAsync();
            await using var command = source.CreateCommand(); command.CommandText = "PRAGMA trusted_schema=OFF"; await command.ExecuteNonQueryAsync();
            await using var pristine = new HouseholdContext(new DbContextOptionsBuilder<HouseholdContext>().UseSqlite("Data Source=:memory:").Options);
            await pristine.Database.OpenConnectionAsync(); await pristine.Database.MigrateAsync();
            if (!(await SchemaAsync(source)).SequenceEqual(await SchemaAsync((SqliteConnection)pristine.Database.GetDbConnection())))
                throw new ArgumentException("The backup schema is incompatible or contains unexpected database objects.");
            command.CommandText = "PRAGMA integrity_check";
            if ((string?)await command.ExecuteScalarAsync() != "ok") throw new ArgumentException("The backup failed its integrity check.");
            command.CommandText = "PRAGMA foreign_key_check";
            await using (var reader = await command.ExecuteReaderAsync()) if (await reader.ReadAsync()) throw new ArgumentException("The backup contains broken references.");
            await using var check = new HouseholdContext(new DbContextOptionsBuilder<HouseholdContext>().UseSqlite(source).Options);
            if (!(await check.Database.GetAppliedMigrationsAsync()).SequenceEqual(check.Database.GetMigrations())) throw new ArgumentException("The backup's schema version is incompatible.");
            var metadata = await check.Set<HouseholdDatabaseMetadata>().AsNoTracking().ToListAsync();
            if (metadata.Count != 1 || metadata[0].HouseholdId != payload.HouseholdId || metadata[0].StorageKey != payload.StorageKey || metadata[0].SchemaVersion != payload.Schema)
                throw new ArgumentException("The backup identity does not match its data.");
            foreach (var table in check.Model.GetEntityTypes().Where(x => x.FindProperty("HouseholdId") != null).Select(x => x.GetTableName()!).Distinct()) {
                command.CommandText = $"SELECT COUNT(*) FROM {Quote(table)} WHERE HouseholdId <> $household"; command.Parameters.Clear(); command.Parameters.AddWithValue("$household", payload.HouseholdId);
                if (Convert.ToInt64(await command.ExecuteScalarAsync()) != 0) throw new ArgumentException("The backup contains another household's data.");
            }
            return (path, await check.Accounts.CountAsync(), await check.Transactions.CountAsync());
        } catch { if (File.Exists(path)) File.Delete(path); throw; }
    }
    public async Task<RecoveryPreview> PreviewAsync(byte[] file, string? password, string currentPassword)
    {
        await AuthorizeAsync(currentPassword);
        var opened = encryption.Decrypt(file, password);
        HouseholdPayload payload;
        try { payload = JsonSerializer.Deserialize<HouseholdPayload>(opened.Plain) ?? throw new JsonException(); }
        catch (JsonException) { throw new ArgumentException("Choose a valid Coiny household backup."); }
        if (!opened.Portable && payload.HouseholdId != Household) throw new ArgumentException("This installation-linked backup belongs to a different household.");
        var validated = await ValidateAsync(payload);
        try {
            var target = await SnapshotAsync(); var id = Guid.NewGuid(); var expiry = Now.AddMinutes(15);
            foreach (var expired in Directory.GetFiles(DirectoryFor("previews")).Where(x => File.GetLastWriteTimeUtc(x) < DateTime.UtcNow.AddMinutes(-30))) {
                try { File.Delete(expired); } catch (IOException) { /* Retry cleanup on a later preview. */ }
            }
            var ticket = new RestoreTicket(UserId, Household, (await RegistryAsync()).StorageKey, target.Sha256, expiry, opened.Portable, payload);
            await WritePrivateAsync(Path.Combine(DirectoryFor("previews"), id.ToString("N")), protection.CreateProtector("Coiny.RestorePreview.v1").Protect(JsonSerializer.SerializeToUtf8Bytes(ticket)));
            return new(id, expiry, payload.CreatedAt, opened.Portable, validated.Accounts, validated.Transactions, await Finance.Accounts.CountAsync(), await Finance.Transactions.CountAsync());
        } finally { File.Delete(validated.Path); }
    }
    public async Task<Guid> RestoreAsync(RestoreRequest input)
    {
        await AuthorizeAsync(input.CurrentPassword ?? "");
        if (!input.ConfirmReplacement) throw new ArgumentException("Confirm replacement of this household's financial data.");
        var ticketPath = Path.Combine(DirectoryFor("previews"), input.PreviewId.ToString("N"));
        if (!File.Exists(ticketPath)) throw new ArgumentException("This preview expired or was already used. Preview the backup again.");
        var ticket = JsonSerializer.Deserialize<RestoreTicket>(protection.CreateProtector("Coiny.RestorePreview.v1").Unprotect(await File.ReadAllBytesAsync(ticketPath)))!;
        var registry = await RegistryAsync();
        if (ticket.UserId != UserId || ticket.HouseholdId != Household || ticket.ExpiresAt < Now || ticket.TargetKey != registry.StorageKey)
            throw new ArgumentException("This preview is no longer valid. Preview the backup again.");
        if (await control.Set<PendingPreferenceChange>().AnyAsync(x => x.HouseholdId == Household && x.CompletedAt == null))
            throw new BudgetConflictException("Finish the pending household settings update before restoring.");
        var safety = await SnapshotAsync();
        if (safety.Sha256 != ticket.TargetHash) throw new BudgetConflictException("Household data changed since your preview. Preview the backup again.");
        var validated = await ValidateAsync(ticket.Payload);
        var key = Guid.NewGuid(); var finalPath = root.DatabasePath(key); var staging = finalPath + ".restore";
        try {
            // Build with trusted migrations. Never activate the uploaded database file.
            await WritePrivateAsync(staging, []);
            await using (var candidateConnection = Connection(staging, SqliteOpenMode.ReadWrite))
            await using (var candidate = new HouseholdContext(new DbContextOptionsBuilder<HouseholdContext>().UseSqlite(candidateConnection).Options)) {
                await candidate.Database.OpenConnectionAsync(); await candidate.Database.MigrateAsync();
                var connection = (SqliteConnection)candidate.Database.GetDbConnection();
                await using (var attach = connection.CreateCommand()) { attach.CommandText = "ATTACH DATABASE $path AS imported"; attach.Parameters.AddWithValue("$path", validated.Path); await attach.ExecuteNonQueryAsync(); }
                await using var transaction = await candidate.Database.BeginTransactionAsync();
                await candidate.Database.ExecuteSqlRawAsync("PRAGMA defer_foreign_keys=ON");
                foreach (var table in candidate.Model.GetEntityTypes().Select(x => x.GetTableName()!).Distinct().Where(x => x != "HouseholdDatabaseMetadata" && x != "PreferenceChangeReceipts" && x != "HouseholdAlertReads")) {
                    var columns = new List<string>();
                    await using (var info = connection.CreateCommand()) {
                        info.Transaction = (SqliteTransaction)transaction.GetDbTransaction(); info.CommandText = $"PRAGMA table_info({Quote(table)})";
                        await using var reader = await info.ExecuteReaderAsync(); while (await reader.ReadAsync()) columns.Add(reader.GetString(1));
                    }
                    var names = string.Join(",", columns.Select(Quote));
                    var values = string.Join(",", columns.Select(x => x == "HouseholdId" ? Household.ToString(CultureInfo.InvariantCulture) : Quote(x)));
                    var copySql = $"INSERT INTO {Quote(table)} ({names}) SELECT {values} FROM imported.{Quote(table)}";
                    await candidate.Database.ExecuteSqlRawAsync(copySql);
                }
                await candidate.Database.ExecuteSqlRawAsync("DELETE FROM sqlite_sequence; INSERT INTO sqlite_sequence SELECT * FROM imported.sqlite_sequence;");
                candidate.Add(new HouseholdDatabaseMetadata { Id = 1, HouseholdId = Household, StorageKey = key, SchemaVersion = HouseholdDatabaseService.CurrentSchema });
                await candidate.SaveChangesAsync(); await transaction.CommitAsync();
                await candidate.Database.ExecuteSqlRawAsync("PRAGMA main.journal_mode=WAL; PRAGMA main.wal_checkpoint(TRUNCATE);");
            }
            File.Move(staging, finalPath, overwrite: false);
            var safetyId = Guid.NewGuid();
            await WritePrivateAsync(Path.Combine(DirectoryFor("safety"), safetyId.ToString("N")), encryption.Encrypt(JsonSerializer.SerializeToUtf8Bytes(safety), null));
            // Both old readers and the durable safety file keep the old state.
            // A crash before this atomic CAS leaves the original registry intact.
            var changed = await control.HouseholdDatabases.Where(x => x.HouseholdId == Household && x.StorageKey == registry.StorageKey && x.State == HouseholdDatabaseState.Ready && control.HouseholdMembers.Any(m => m.HouseholdId == Household && m.UserId == UserId && m.Role == HouseholdRole.Owner))
                .ExecuteUpdateAsync(x => x.SetProperty(r => r.StorageKey, key));
            if (changed != 1) throw new BudgetConflictException("Household storage changed. Preview the backup again.");
            return safetyId;
        } finally {
            File.Delete(validated.Path);
            if (File.Exists(staging)) File.Delete(staging);
            // Never delete a published file after an ambiguous PostgreSQL error:
            // its registry update may have committed even if the reply was lost.
        }
    }
    public async Task<object[]> SafetyListAsync()
    {
        await AuthorizeAsync(); var list = new List<object>();
        foreach (var path in Directory.GetFiles(DirectoryFor("safety")).OrderByDescending(File.GetCreationTimeUtc).Take(10))
            if (Guid.TryParseExact(Path.GetFileName(path), "N", out var id)) list.Add(new { Id = id, CreatedAt = File.GetCreationTimeUtc(path) });
        return list.ToArray();
    }
    public async Task<byte[]> SafetyAsync(Guid id, string password) { await AuthorizeAsync(password); var path = Path.Combine(DirectoryFor("safety"), id.ToString("N")); if (!File.Exists(path)) throw new ArgumentException("Safety backup is no longer available."); return await File.ReadAllBytesAsync(path); }
    public async Task<byte[]> ExportAsync(string password)
    {
        await AuthorizeAsync(password); using var bytes = new MemoryStream();
        using (var zip = new ZipArchive(bytes, ZipArchiveMode.Create, true)) {
            foreach (var table in Finance.Model.GetEntityTypes().Select(x => x.GetTableName()!).Distinct().Where(x => x != "HouseholdDatabaseMetadata" && x != "PreferenceChangeReceipts").Order()) {
                var entry = zip.CreateEntry(table + ".csv"); await using var output = entry.Open(); await using var text = new StreamWriter(output, new UTF8Encoding(true));
                await using var command = Finance.Database.GetDbConnection().CreateCommand(); command.CommandText = $"SELECT * FROM {Quote(table)}";
                await using var reader = await command.ExecuteReaderAsync(); await text.WriteLineAsync(string.Join(",", Enumerable.Range(0, reader.FieldCount).Select(i => Csv(reader.GetName(i)))));
                while (await reader.ReadAsync()) await text.WriteLineAsync(string.Join(",", Enumerable.Range(0, reader.FieldCount).Select(i => Csv(reader.IsDBNull(i) ? "" : Convert.ToString(reader.GetValue(i), CultureInfo.InvariantCulture) ?? ""))));
            }
            var balances = zip.CreateEntry("AccountBalances.csv");
            await using (var output = new StreamWriter(balances.Open(), new UTF8Encoding(true))) {
                await output.WriteLineAsync("Account ID,Account,Type,Current balance,Opening balance,Opening date,Active");
                foreach (var account in await new AccountService(Finance, current, new AccountBalanceService(Finance), new AccountBalanceSnapshotService(Finance, current)).GetAccountsAsync())
                    await output.WriteLineAsync(string.Join(",", new[] { account.Id.ToString(), account.Name, account.AccountType.ToString(), account.CurrentBalance.ToString(CultureInfo.InvariantCulture), account.OpeningBalance.ToString(CultureInfo.InvariantCulture), account.OpeningBalanceDate.ToString("yyyy-MM-dd"), account.IsActive.ToString() }.Select(Csv)));
            }
            var ledger = zip.CreateEntry("TransactionLedger.csv");
            await using (var output = new StreamWriter(ledger.Open(), new UTF8Encoding(true))) {
                await output.WriteLineAsync("Transaction ID,Date,Account,Category,Type,Merchant,Description,Amount,Transfer direction,Cleared,Notes");
                foreach (var entry in await Finance.Transactions.AsNoTracking().Include(x => x.Account).Include(x => x.Category).OrderBy(x => x.TransactionDate).ThenBy(x => x.Id).ToListAsync())
                    await output.WriteLineAsync(string.Join(",", new[] { entry.Id.ToString(), entry.TransactionDate.ToString("yyyy-MM-dd"), entry.Account.Name, entry.Category.Name, entry.Category.CategoryType.ToString(), entry.Merchant ?? "", entry.Description, entry.Amount.ToString(CultureInfo.InvariantCulture), entry.TransferDirection?.ToString() ?? "", entry.IsCleared.ToString(), entry.Notes ?? "" }.Select(Csv)));
            }
            var readme = zip.CreateEntry("README.txt"); await using var stream = new StreamWriter(readme.Open());
            await stream.WriteAsync("Coiny household CSV export. These files are readable and NOT encrypted. Keep them private. IDs link the files together; financial amounts keep their recorded sign and account/category type. This export is not a restorable Coiny backup. User accounts, passwords and memberships are excluded.");
        }
        return bytes.ToArray();
    }
    internal static string Csv(string value)
    {
        var trimmed = value.TrimStart();
        if (trimmed.Length > 0 && "=+-@\t\r\n".Contains(trimmed[0]) && !decimal.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out _)) value = "'" + value;
        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }
}
