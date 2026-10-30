using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Coiny.API.Data.Hybrid;

internal sealed record HouseholdBackupManifest(int Format, int HouseholdId, Guid StorageKey, int SchemaVersion, string FileName, string Sha256);

// Operational building block, not a public endpoint or online overwrite operation.
// Restores publish only into empty storage; central identity is never imported.
internal static class HouseholdBackupArchive
{
    internal static async Task<HouseholdBackupManifest> CreateAsync(HouseholdDatabaseService storage, int householdId, string directory)
    {
        Directory.CreateDirectory(directory);
        using var gate = await storage.AcquireWriterAsync(householdId, CancellationToken.None);
        await using var source = await storage.OpenForWorkerAsync(householdId);
        var metadata = await source.Set<HouseholdDatabaseMetadata>().AsNoTracking().SingleAsync();
        var name = Guid.NewGuid().ToString("N") + ".sqlite"; var path = Path.Combine(directory, name);
        using (new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None)) { }
        Private(path);
        await using (var destination = Connection(path, SqliteOpenMode.ReadWrite)) {
            await destination.OpenAsync(); ((SqliteConnection)source.Database.GetDbConnection()).BackupDatabase(destination);
        }
        var manifest = new HouseholdBackupManifest(1, householdId, metadata.StorageKey, metadata.SchemaVersion, name, await DigestAsync(path));
        var manifestPath = path + ".json";
        await using (var output = new FileStream(manifestPath, FileMode.CreateNew, FileAccess.Write, FileShare.None)) {
            Private(manifestPath); await JsonSerializer.SerializeAsync(output, manifest);
        }
        return manifest;
    }

    internal static async Task RestoreToEmptyStorageAsync(string archiveDirectory, HouseholdBackupManifest manifest,
        HybridStorageRoot destination, HouseholdDatabase expected, Func<Task>? beforePublish = null)
    {
        // Bind the backup to the live registry; an editable manifest cannot retarget
        // another household or smuggle in arbitrary paths.
        if (manifest.Format != 1 || manifest.HouseholdId != expected.HouseholdId || manifest.StorageKey != expected.StorageKey ||
            manifest.SchemaVersion != HouseholdDatabaseService.CurrentSchema || expected.SchemaVersion != manifest.SchemaVersion ||
            expected.OwnerNode != destination.Node || manifest.FileName != Path.GetFileName(manifest.FileName) ||
            !Guid.TryParseExact(Path.GetFileNameWithoutExtension(manifest.FileName), "N", out _) || Path.GetExtension(manifest.FileName) != ".sqlite")
            throw new InvalidOperationException("Backup identity, version or path is invalid.");
        var sourcePath = Path.Combine(archiveDirectory, manifest.FileName);
        if (await DigestAsync(sourcePath) != manifest.Sha256) throw new InvalidOperationException("Backup checksum failed.");
        await using (var source = Connection(sourcePath, SqliteOpenMode.ReadOnly)) {
            await source.OpenAsync();
            await using var check = source.CreateCommand(); check.CommandText = "PRAGMA integrity_check";
            if ((string?)await check.ExecuteScalarAsync() != "ok") throw new InvalidOperationException("Backup integrity check failed.");
            check.CommandText = "PRAGMA foreign_key_check";
            await using (var reader = await check.ExecuteReaderAsync()) if (await reader.ReadAsync()) throw new InvalidOperationException("Backup foreign-key check failed.");
            await using (var schema = new HouseholdContext(new DbContextOptionsBuilder<HouseholdContext>().UseSqlite(source).Options)) {
                var applied = await schema.Database.GetAppliedMigrationsAsync();
                if (!applied.SequenceEqual(schema.Database.GetMigrations())) throw new InvalidOperationException("Backup schema history is incompatible.");
            }
            check.CommandText = "SELECT HouseholdId, StorageKey, SchemaVersion FROM HouseholdDatabaseMetadata WHERE Id=1";
            await using var identity = await check.ExecuteReaderAsync();
            if (!await identity.ReadAsync() || identity.GetInt32(0) != manifest.HouseholdId || identity.GetGuid(1) != manifest.StorageKey || identity.GetInt32(2) != manifest.SchemaVersion)
                throw new InvalidOperationException("Backup file identity does not match its manifest.");
        }
        var finalPath = destination.DatabasePath(expected.StorageKey);
        if (File.Exists(finalPath)) throw new IOException("Restore requires empty destination storage; existing data was not changed.");
        var staging = finalPath + ".restore-" + Guid.NewGuid().ToString("N");
        try {
            using (new FileStream(staging, FileMode.CreateNew, FileAccess.Write, FileShare.None)) { }
            Private(staging);
            await using (var source = Connection(sourcePath, SqliteOpenMode.ReadOnly))
            await using (var target = Connection(staging, SqliteOpenMode.ReadWrite)) {
                await source.OpenAsync(); await target.OpenAsync(); source.BackupDatabase(target);
            }
            if (beforePublish != null) await beforePublish();
            File.Move(staging, finalPath, overwrite: false);
        } finally { if (File.Exists(staging)) File.Delete(staging); }
    }

    private static SqliteConnection Connection(string path, SqliteOpenMode mode) => new(new SqliteConnectionStringBuilder {
        DataSource = path, Mode = mode, Pooling = false, ForeignKeys = true }.ToString());
    internal static async Task<string> DigestAsync(string path)
    {
        await using var stream = File.OpenRead(path); return Convert.ToHexString(await SHA256.HashDataAsync(stream));
    }
    private static void Private(string path) { if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite); }
}
