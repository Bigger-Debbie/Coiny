using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Coiny.API.Data.Hybrid;

internal sealed record LegacyMigrationPlan(int Format, string SourceSha256, int Users, int Households,
    Dictionary<string, long> TableCounts, string[] SourceMigrations);

// Offline operator commands. Never invoked by an HTTP request or application worker.
internal static class LegacyMigrationCommand
{
    internal static async Task<LegacyMigrationPlan> PlanAsync(string snapshot)
    {
        if (!Path.IsPathFullyQualified(snapshot) || !File.Exists(snapshot))
            throw new InvalidOperationException("An existing absolute snapshot path is required.");
        if ((File.Exists(snapshot + "-wal") && new FileInfo(snapshot + "-wal").Length != 0) || File.Exists(snapshot + "-journal"))
            throw new InvalidOperationException("Use a closed standalone snapshot, not a live database with journal state.");
        var sha = await HouseholdBackupArchive.DigestAsync(snapshot);
        await using var source = Open(snapshot);
        await LegacyMigrationEngine.ValidateSnapshotAsync(source);
        if ((await source.Database.GetAppliedMigrationsAsync()).Except(source.Database.GetMigrations()).Any())
            throw new InvalidOperationException("Snapshot contains unknown migrations; use its matching application release.");
        var integrity = await source.Database.SqlQueryRaw<string>("PRAGMA integrity_check").ToListAsync();
        if (integrity.Count != 1 || integrity[0] != "ok") throw new InvalidOperationException("Source integrity check failed.");
        await source.Database.OpenConnectionAsync();
        var counts = new Dictionary<string, long>();
        foreach (var table in source.Model.GetEntityTypes().Select(x => x.GetTableName()!).Distinct().Order()) {
            await using var command = source.Database.GetDbConnection().CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM \"" + table.Replace("\"", "\"\"") + "\"";
            counts.Add(table, Convert.ToInt64(await command.ExecuteScalarAsync()));
        }
        if (sha != await HouseholdBackupArchive.DigestAsync(snapshot)) throw new InvalidOperationException("Snapshot changed during inspection.");
        return new(1, sha, await source.Users.CountAsync(), await source.Set<Models.Household>().CountAsync(), counts,
            (await source.Database.GetAppliedMigrationsAsync()).ToArray());
    }

    internal static async Task ApplyAsync(string snapshot, string expectedSha, IServiceProvider services)
    {
        var plan = await PlanAsync(snapshot);
        if (!string.Equals(expectedSha, plan.SourceSha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Snapshot digest differs from the reviewed plan.");
        // Acquiring the root lock first refuses concurrent API/worker ownership.
        _ = services.GetRequiredService<HybridStorageRoot>();
        await using var scope = services.CreateAsyncScope();
        var control = scope.ServiceProvider.GetRequiredService<ControlContext>();
        await control.Database.MigrateAsync();
        await using var source = Open(snapshot);
        await LegacyMigrationEngine.ImportAsync(source, control,
            services.GetRequiredService<HouseholdDatabaseService>(), snapshot, plan.SourceSha256);
    }

    internal static async Task<int> RunAsync(string[] args)
    {
        try {
            if (args is ["--migration-plan", var snapshot, var report] && Path.IsPathFullyQualified(report)) {
                var plan = await PlanAsync(snapshot);
                var options = new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write, Share = FileShare.None };
                if (!OperatingSystem.IsWindows()) options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
                await using var output = new FileStream(report, options);
                await JsonSerializer.SerializeAsync(output, plan, new JsonSerializerOptions { WriteIndented = true });
                Console.WriteLine($"Read-only migration plan verified: {plan.Users} users, {plan.Households} households. No destination was opened.");
                return 0;
            }
            if (args is ["--migrate-snapshot", var input, var sha, "--offline"]) {
                var builder = Host.CreateApplicationBuilder(Array.Empty<string>());
                builder.Services.AddHybridStorageFoundation(builder.Configuration);
                using var host = builder.Build(); // Deliberately never StartAsync: no API or jobs.
                await ApplyAsync(input, sha, host.Services);
                Console.WriteLine("Offline migration verified. Original snapshot retained; users must sign in again.");
                return 0;
            }
            throw new InvalidOperationException("Use --migration-plan ABSOLUTE_SNAPSHOT ABSOLUTE_NEW_REPORT or --migrate-snapshot ABSOLUTE_SNAPSHOT REVIEWED_SHA256 --offline.");
        } catch (Exception error) {
            // No provider errors, source values, connection strings or secrets in CLI output.
            Console.Error.WriteLine($"Offline migration failed ({error.GetType().Name}). Check the reviewed plan, configuration and offline destination; resume with the identical snapshot.");
            return 1;
        }
    }

    private static CoinyContext Open(string path) => new(new DbContextOptionsBuilder<CoinyContext>().UseSqlite(
        new SqliteConnectionStringBuilder { DataSource = path, Mode = SqliteOpenMode.ReadOnly, Pooling = false }.ToString()).Options);
}
