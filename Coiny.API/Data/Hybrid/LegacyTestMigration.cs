using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Coiny.API.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Coiny.API.Data.Hybrid;

// Deliberately limited to the local Docker test deployment. The general-purpose
// migration/export CLI and real-data rehearsal are separate plan milestones.
public static class LegacyTestMigration
{
    public static async Task RunAsync(IServiceProvider services, IConfiguration config, IHostEnvironment environment)
    {
        var settings = services.GetRequiredService<Microsoft.Extensions.Options.IOptions<HybridStorageOptions>>().Value;
        var pg = new Npgsql.NpgsqlConnectionStringBuilder(settings.ControlConnection);
        var sourceConfig = new SqliteConnectionStringBuilder(config.GetConnectionString("DefaultConnection"));
        if (!environment.IsDevelopment() || pg.Host != "postgres" || pg.Database != "coiny_test_control" || pg.Username != "coiny_test" ||
            sourceConfig.DataSource != "/data/coiny-test.db" || settings.DataRoot != "/data/households")
            throw new InvalidOperationException("This migration command is restricted to the local Docker test stack.");
        // Durable on the named volume: losing a worktree's selector must never
        // restart the stale combined database and accept divergent writes.
        await File.WriteAllTextAsync("/data/.hybrid-migration-started", "Use verified hybrid mode or resume the explicit migration.\n");
        await using var scope = services.CreateAsyncScope();
        var control = scope.ServiceProvider.GetRequiredService<ControlContext>();
        await control.Database.MigrateAsync();
        var existing = await control.Set<LegacyImportState>().SingleOrDefaultAsync();
        if (existing?.CompletedAt != null) { Console.WriteLine("Docker test migration already verified; preserving split data."); return; }
        var snapshot = "/data/coiny-before-hybrid.db";
        if (!File.Exists(snapshot)) {
            if (!File.Exists(sourceConfig.DataSource)) throw new InvalidOperationException("The Docker test source database is missing.");
            await using var sourceConnection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = sourceConfig.DataSource, Mode = SqliteOpenMode.ReadOnly, Pooling = false }.ToString());
            await using var destinationConnection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = snapshot, Pooling = false }.ToString());
            await sourceConnection.OpenAsync(); await destinationConnection.OpenAsync(); sourceConnection.BackupDatabase(destinationConnection);
        }
        // Reject resumed migration if someone restarted the old API and changed its
        // data after the retained snapshot. Never silently activate an older copy.
        await VerifySourceUnchangedAsync(sourceConfig.DataSource, snapshot);
        var sha = Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(snapshot)));
        if (existing != null && existing.SourceSha256 != sha) throw new InvalidOperationException("The migration source changed; refusing to mix snapshots.");
        await using var legacy = new CoinyContext(new DbContextOptionsBuilder<CoinyContext>().UseSqlite(new SqliteConnectionStringBuilder {
            DataSource = snapshot, Mode = SqliteOpenMode.ReadOnly, Pooling = false }.ToString()).Options);
        if ((await legacy.Database.GetPendingMigrationsAsync()).Any()) throw new InvalidOperationException("The source has unapplied legacy migrations.");
        await LegacyMigrationEngine.ImportAsync(legacy, control,
            scope.ServiceProvider.GetRequiredService<HouseholdDatabaseService>(), snapshot, sha);
        var households = await control.Set<Household>().CountAsync();
        Console.WriteLine($"Docker test migration verified: {await control.Users.CountAsync()} users, {households} households. Original and snapshot retained; sign in again.");
    }

    private static async Task VerifySourceUnchangedAsync(string source, string snapshot)
    {
        if (!File.Exists(source)) throw new InvalidOperationException("Original test database is missing.");
        await using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = snapshot, Mode = SqliteOpenMode.ReadOnly, Pooling = false }.ToString());
        await connection.OpenAsync();
        await using (var attach = connection.CreateCommand()) { attach.CommandText = "ATTACH DATABASE $source AS current_source"; attach.Parameters.AddWithValue("$source", source); await attach.ExecuteNonQueryAsync(); }
        var tables = new List<string>();
        await using (var command = connection.CreateCommand()) {
            command.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%'";
            await using var reader = await command.ExecuteReaderAsync(); while (await reader.ReadAsync()) tables.Add(reader.GetString(0));
        }
        await using var tx = connection.BeginTransaction(deferred: true);
        foreach (var table in tables) {
            var before = await RowsAsync(connection, $"SELECT * FROM {Quote(table)}", 0, tx);
            var current = await RowsAsync(connection, $"SELECT * FROM current_source.{Quote(table)}", 0, tx);
            if (!before.SequenceEqual(current)) throw new InvalidOperationException("The original test database changed after its snapshot. Refusing to activate stale data.");
        }
    }

    private static string Quote(string name) => "\"" + name.Replace("\"", "\"\"") + "\"";

    internal static Task<List<string>> RowsAsync(SqliteConnection connection, string sql, int household,
        SqliteTransaction tx, IReadOnlySet<string>? decimalColumns = null) =>
        LegacyMigrationEngine.RowsAsync(connection, sql, household, tx, decimalColumns);
}
