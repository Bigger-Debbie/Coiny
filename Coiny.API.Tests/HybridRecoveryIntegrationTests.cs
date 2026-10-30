using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics;
using System.Security.Claims;
using Coiny.API.Data.Hybrid;
using Coiny.API.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Coiny.API.Tests;

public class HybridRecoveryIntegrationTests
{
    private sealed class Interrupted : Exception;

    [PostgresFact]
    public async Task Household_selection_excludes_other_users_and_finances_and_preserves_selected_results()
    {
        await using var source = await HybridMigrationScenario.CreateAsync();
        var directory = TemporaryDirectory();
        var destination = new HybridStorageIntegrationTests(); await destination.InitializeAsync();
        try {
            var selected = Path.Combine(directory, "selected.sqlite");
            await OperatorTool("python3", "/operations/household_snapshot.py", "--source", source.PathName,
                "--email", "alice@coiny.test", "--output", selected);
            var plan = await LegacyMigrationCommand.PlanAsync(selected);
            Assert.Equal(1, plan.Users); Assert.Equal(1, plan.Households);
            await using var legacy = new Coiny.API.Data.CoinyContext(new DbContextOptionsBuilder<Coiny.API.Data.CoinyContext>()
                .UseSqlite(new SqliteConnectionStringBuilder { DataSource = selected, Mode = SqliteOpenMode.ReadOnly, Pooling = false }.ToString()).Options);
            Assert.False(await legacy.Users.AnyAsync(x => x.Id == "bob"));
            Assert.False(await legacy.Transactions.AnyAsync(x => x.HouseholdId != 1));
            Assert.Equal(source.Expected, await HybridMigrationScenario.CaptureAsync(legacy, legacy, source.Clock));
            await using var control = await destination.factory.CreateDbContextAsync();
            await LegacyMigrationEngine.ImportAsync(legacy, control, destination.storage, selected, plan.SourceSha256);
            await using var household = await destination.storage.OpenForWorkerAsync(1);
            Assert.Equal(source.Expected, await HybridMigrationScenario.CaptureAsync(household, control, source.Clock));
            Assert.Single(await control.Users.ToListAsync()); Assert.Single(await control.HouseholdDatabases.ToListAsync());
            var fixedDate = new DateOnly(2026, 10, 16);
            var beforeGroups = await PersonalRehearsalCommand.CaptureAsync(legacy, legacy, "alice", 1, fixedDate);
            var afterGroups = await PersonalRehearsalCommand.CaptureAsync(household, control, "alice", 1, fixedDate);
            Assert.Equal(beforeGroups.OrderBy(x => x.Key), afterGroups.OrderBy(x => x.Key));
            Assert.Equal(source.Sha, await HouseholdBackupArchive.DigestAsync(source.PathName));
        } finally { await destination.DisposeAsync(); Directory.Delete(directory, true); }
    }

    [PostgresFact]
    public async Task General_offline_plan_and_apply_preserve_source_and_refuse_unreviewed_digest()
    {
        await using var source = await HybridMigrationScenario.CreateAsync();
        var plan = await LegacyMigrationCommand.PlanAsync(source.PathName);
        Assert.Equal(source.Sha, plan.SourceSha256);
        Assert.Equal(2, plan.Users); Assert.Equal(2, plan.Households);
        Assert.True(plan.TableCounts["Transactions"] > 0);
        var report = Path.Combine(Path.GetDirectoryName(source.PathName)!, "plan-" + Guid.NewGuid().ToString("N") + ".json");
        try {
            Assert.Equal(0, await LegacyMigrationCommand.RunAsync(["--migration-plan", source.PathName, report]));
            Assert.Equal(1, await LegacyMigrationCommand.RunAsync(["--migration-plan", source.PathName, report]));
            Assert.DoesNotContain("Synthetic!2026", await File.ReadAllTextAsync(report));
            if (!OperatingSystem.IsWindows()) Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(report));
        } finally { if (File.Exists(report)) File.Delete(report); }
        var destination = new HybridStorageIntegrationTests(); await destination.InitializeAsync();
        try {
            var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
            services.AddSingleton(destination.factory); services.AddSingleton(destination.root); services.AddSingleton(destination.storage);
            services.AddScoped(_ => destination.factory.CreateDbContext());
            await using var provider = services.BuildServiceProvider();
            await Assert.ThrowsAsync<InvalidOperationException>(() => LegacyMigrationCommand.ApplyAsync(source.PathName, new string('0', 64), provider));
            await using (var empty = await destination.factory.CreateDbContextAsync()) Assert.False(await empty.Users.AnyAsync());
            await LegacyMigrationCommand.ApplyAsync(source.PathName, plan.SourceSha256, provider);
            await using (var different = await HybridMigrationScenario.CreateAsync())
                await Assert.ThrowsAsync<InvalidOperationException>(() => LegacyMigrationCommand.ApplyAsync(different.PathName, different.Sha, provider));
            await using var control = await destination.factory.CreateDbContextAsync();
            await using var household = await destination.storage.OpenForWorkerAsync(1);
            Assert.Equal(source.Expected, await HybridMigrationScenario.CaptureAsync(household, control, source.Clock));
            Assert.Equal(source.Sha, await HouseholdBackupArchive.DigestAsync(source.PathName));
        } finally { await destination.DisposeAsync(); }
    }

    [PostgresFact]
    public async Task Rich_migration_resumes_each_commit_boundary_and_preserves_financial_outputs_and_deleted_IDs()
    {
        foreach (var phase in new[] { "ControlCommitted", "HouseholdCommitted", "BeforeActivation" }) {
            await using var source = await HybridMigrationScenario.CreateAsync();
            var destination = new HybridStorageIntegrationTests(); await destination.InitializeAsync();
            try {
                await using (var central = await destination.factory.CreateDbContextAsync()) {
                    await Assert.ThrowsAsync<Interrupted>(() => LegacyMigrationEngine.ImportAsync(source.Source, central, destination.storage,
                        source.PathName, source.Sha, point => point.Phase == phase ? Task.FromException(new Interrupted()) : Task.CompletedTask));
                }
                await using var resumed = await destination.factory.CreateDbContextAsync();
                Assert.Null((await resumed.Set<LegacyImportState>().SingleAsync()).CompletedAt);
                await LegacyMigrationEngine.ImportAsync(source.Source, resumed, destination.storage, source.PathName, source.Sha);
                await using var household = await destination.storage.OpenForWorkerAsync(1);
                Assert.Equal(source.Expected, await HybridMigrationScenario.CaptureAsync(household, resumed, source.Clock));
                Assert.Equal(2, await resumed.Users.CountAsync()); Assert.Equal(2, await resumed.HouseholdDatabases.CountAsync());
                Assert.Equal(source.Sha, await HouseholdBackupArchive.DigestAsync(source.PathName));
                var extra = new Transaction { HouseholdId = 1, AccountId = 1, CategoryId = 1, Amount = 1.01m, Description = "After migration", TransactionDate = new(2026, 10, 16) };
                household.Add(extra); await household.SaveChangesAsync(); Assert.True(extra.Id > 10000);
                await LegacyMigrationEngine.ImportAsync(source.Source, resumed, destination.storage, source.PathName, source.Sha);
                Assert.True(await household.Transactions.AnyAsync(x => x.Id == extra.Id));
            } finally { await destination.DisposeAsync(); }
        }
    }

    [PostgresFact]
    public async Task Cross_household_source_reference_is_rejected_before_destination_data_is_written()
    {
        await using var source = await HybridMigrationScenario.CreateAsync();
        await using (var corrupt = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = source.PathName, Pooling = false }.ToString())) {
            await corrupt.OpenAsync(); await using var command = corrupt.CreateCommand();
            command.CommandText = "UPDATE Transactions SET AccountId=3 WHERE Id=1"; await command.ExecuteNonQueryAsync();
        }
        var destination = new HybridStorageIntegrationTests(); await destination.InitializeAsync();
        try {
            await using var central = await destination.factory.CreateDbContextAsync();
            var changedSha = await HouseholdBackupArchive.DigestAsync(source.PathName);
            await Assert.ThrowsAsync<InvalidOperationException>(() => LegacyMigrationEngine.ImportAsync(source.Source, central, destination.storage, source.PathName, changedSha));
            Assert.Empty(await central.Users.ToListAsync()); Assert.Empty(await central.HouseholdDatabases.ToListAsync());
            Assert.Empty(Directory.GetFiles(destination.root.DirectoryPath, "*.db"));
        } finally { await destination.DisposeAsync(); }
    }

    [PostgresFact]
    public async Task Financial_restore_rejects_bad_archives_resumes_before_publish_and_keeps_current_revocation()
    {
        var source = new HybridStorageIntegrationTests(); await source.InitializeAsync();
        var artifacts = TemporaryDirectory(); var restoredDirectory = TemporaryDirectory();
        try {
            var a = await source.HouseholdAsync("alice"); var b = await source.HouseholdAsync("bob");
            await source.storage.ProvisionAsync(a.Id); await source.storage.ProvisionAsync(b.Id);
            await using (var financial = await source.storage.OpenForSessionAsync(a.Principal, a.Id)) {
                financial.Add(new Account { HouseholdId = a.Id, Name = "Keep me", OpeningBalance = 123.45m, OpeningBalanceDate = new(2026, 1, 1) }); await financial.SaveChangesAsync();
            }
            var archive = await HouseholdBackupArchive.CreateAsync(source.storage, a.Id, artifacts);
            await using var central = await source.factory.CreateDbContextAsync();
            var registry = await central.HouseholdDatabases.AsNoTracking().SingleAsync(x => x.HouseholdId == a.Id);
            var other = await central.HouseholdDatabases.AsNoTracking().SingleAsync(x => x.HouseholdId == b.Id);
            await central.HouseholdMembers.Where(x => x.UserId == a.UserId).ExecuteDeleteAsync();
            using var restoredRoot = new HybridStorageRoot(restoredDirectory, source.root.Node);
            await Assert.ThrowsAsync<InvalidOperationException>(() => HouseholdBackupArchive.RestoreToEmptyStorageAsync(artifacts, archive, restoredRoot, other));
            await Assert.ThrowsAsync<InvalidOperationException>(() => HouseholdBackupArchive.RestoreToEmptyStorageAsync(artifacts, archive with { Format = 999 }, restoredRoot, registry));
            await Assert.ThrowsAsync<InvalidOperationException>(() => HouseholdBackupArchive.RestoreToEmptyStorageAsync(artifacts, archive with { SchemaVersion = 999 }, restoredRoot, registry));
            await Assert.ThrowsAsync<InvalidOperationException>(() => HouseholdBackupArchive.RestoreToEmptyStorageAsync(artifacts, archive with { HouseholdId = b.Id, StorageKey = other.StorageKey }, restoredRoot, other));
            await Assert.ThrowsAsync<InvalidOperationException>(() => HouseholdBackupArchive.RestoreToEmptyStorageAsync(artifacts, archive with { FileName = "../outside.sqlite" }, restoredRoot, registry));
            var corruptName = Guid.NewGuid().ToString("N") + ".sqlite";
            File.Copy(Path.Combine(artifacts, archive.FileName), Path.Combine(artifacts, corruptName)); await File.AppendAllTextAsync(Path.Combine(artifacts, corruptName), "corrupt");
            await Assert.ThrowsAsync<InvalidOperationException>(() => HouseholdBackupArchive.RestoreToEmptyStorageAsync(artifacts, archive with { FileName = corruptName }, restoredRoot, registry));
            Assert.Empty(Directory.GetFiles(restoredDirectory, "*.db"));
            await Assert.ThrowsAsync<Interrupted>(() => HouseholdBackupArchive.RestoreToEmptyStorageAsync(artifacts, archive, restoredRoot, registry, () => Task.FromException(new Interrupted())));
            Assert.Empty(Directory.GetFiles(restoredDirectory, "*.restore-*")); Assert.Empty(Directory.GetFiles(restoredDirectory, "*.db"));
            await HouseholdBackupArchive.RestoreToEmptyStorageAsync(artifacts, archive, restoredRoot, registry);
            var restored = new HouseholdDatabaseService(source.factory, restoredRoot, TimeProvider.System);
            await Assert.ThrowsAsync<HouseholdStorageAccessDeniedException>(() => restored.OpenForSessionAsync(a.Principal, a.Id));
            await using (var financial = await restored.OpenForWorkerAsync(a.Id)) Assert.Equal(123.45m, (await financial.Accounts.SingleAsync()).OpeningBalance);
            var before = await HouseholdBackupArchive.DigestAsync(restoredRoot.DatabasePath(registry.StorageKey));
            await Assert.ThrowsAsync<IOException>(() => HouseholdBackupArchive.RestoreToEmptyStorageAsync(artifacts, archive, restoredRoot, registry));
            Assert.Equal(before, await HouseholdBackupArchive.DigestAsync(restoredRoot.DatabasePath(registry.StorageKey)));
            Assert.False(await central.HouseholdMembers.AnyAsync(x => x.UserId == a.UserId));
        } finally { await source.DisposeAsync(); Directory.Delete(artifacts, true); Directory.Delete(restoredDirectory, true); }
    }

    [PostgresFact]
    public async Task Pg_dump_and_household_backups_recover_into_fresh_storage_with_passwords_and_financial_results_intact()
    {
        await using var scenario = await HybridMigrationScenario.CreateAsync();
        var source = new HybridStorageIntegrationTests(); await source.InitializeAsync();
        var artifacts = TemporaryDirectory(); var restoredDirectory = TemporaryDirectory();
        var bundleDirectory = TemporaryDirectory();
        var targetName = "coiny_recovery_" + Guid.NewGuid().ToString("N"); NpgsqlConnectionStringBuilder? target = null;
        try {
            await using var central = await source.factory.CreateDbContextAsync();
            await LegacyMigrationEngine.ImportAsync(scenario.Source, central, source.storage, scenario.PathName, scenario.Sha);
            var session = new AuthSession { UserId = "alice", CreatedAt = DateTime.UtcNow, ExpiresAt = DateTime.UtcNow.AddHours(1) };
            central.Add(session); central.Add(new AuthRecoveryCode { UserId = "alice", CodeHash = new string('b', 64), CreatedAt = DateTime.UtcNow }); await central.SaveChangesAsync();
            var originalUser = await central.Users.AsNoTracking().SingleAsync(x => x.Id == "alice");
            var principal = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, "alice"), new Claim("jti", session.Id), new Claim("security_stamp", originalUser.SecurityStamp!) }, "test"));
            await using (var authenticated = await source.storage.OpenForSessionAsync(principal, 1)) Assert.NotEmpty(await authenticated.Accounts.ToListAsync());
            var maintenance = await HybridMaintenanceIntegrationTests.GateAsync(source);
            var backupOperation = Guid.NewGuid(); await maintenance.RequestAsync(backupOperation);
            await using (var drained = await maintenance.DrainAsync(backupOperation)) { }
            // This fixture has no running API or worker: all stores are quiescent for
            // the entire coordinated snapshot, not merely consistent individually.
            var archives = new List<HouseholdBackupManifest>();
            foreach (var id in await central.HouseholdDatabases.Select(x => x.HouseholdId).ToArrayAsync()) archives.Add(await HouseholdBackupArchive.CreateAsync(source.storage, id, artifacts));
            var connection = new NpgsqlConnectionStringBuilder(central.Database.GetConnectionString());
            var dump = Path.Combine(artifacts, "control.dump");
            using (File.Create(dump)) { } if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(dump, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            await PgTool("pg_dump", connection, "--format=custom", "--file", dump);
            var dumpSha = await HouseholdBackupArchive.DigestAsync(dump);
            // Exercise the actual encrypted package, then recover exclusively from
            // its decrypted/verified output (not the original plaintext artifacts).
            var key = Path.Combine(bundleDirectory, "identity");
            await OperatorTool("age-keygen", "-o", key);
            var recipient = (await OperatorTool("age-keygen", "-y", key)).Trim();
            var package = Path.Combine(bundleDirectory, "backup.age");
            await OperatorTool("python3", "/operations/backup_bundle.py", "pack", "--snapshots", artifacts,
                "--output", package, "--recipient", recipient, "--release", "synthetic-integration", "--key-id", "test-v1");
            var decrypted = Path.Combine(bundleDirectory, "verified");
            await OperatorTool("python3", "/operations/backup_bundle.py", "unpack", "--package", package,
                "--destination", decrypted, "--identity", key);
            Directory.Delete(artifacts, true);
            artifacts = decrypted; dump = Path.Combine(artifacts, "control.dump");
            target = new NpgsqlConnectionStringBuilder(connection.ConnectionString) { Database = targetName, Pooling = false };
            await using (var admin = new NpgsqlConnection(connection.ConnectionString)) {
                await admin.OpenAsync(); await using var create = new NpgsqlCommand($"CREATE DATABASE {targetName}", admin); await create.ExecuteNonQueryAsync();
            }
            Assert.Equal(dumpSha, await HouseholdBackupArchive.DigestAsync(dump));
            await PgTool("pg_restore", target, "--no-owner", "--no-privileges", "--exit-on-error", dump);
            var factory = new RestoredFactory(target.ConnectionString);
            await using var recovered = factory.CreateDbContext();
            Assert.Empty(await recovered.Database.GetPendingMigrationsAsync());
            var recoveredGate = new InstallationMaintenance(Microsoft.Extensions.Options.Options.Create(new HybridStorageOptions { ControlConnection = target.ConnectionString }));
            Assert.Equal(backupOperation, await recoveredGate.StatusAsync());
            await Assert.ThrowsAsync<InstallationMaintenanceException>(() => recoveredGate.EnterAsync());
            await recovered.HouseholdDatabases.ExecuteUpdateAsync(x => x.SetProperty(r => r.OwnerNode, "restored-node"));
            using var root = new HybridStorageRoot(restoredDirectory, "restored-node");
            foreach (var archive in archives) {
                var registry = await recovered.HouseholdDatabases.AsNoTracking().SingleAsync(x => x.HouseholdId == archive.HouseholdId);
                await HouseholdBackupArchive.RestoreToEmptyStorageAsync(artifacts, archive, root, registry);
            }
            var storage = new HouseholdDatabaseService(factory, root, TimeProvider.System);
            await using (var financial = await storage.OpenForWorkerAsync(1)) Assert.Equal(scenario.Expected, await HybridMigrationScenario.CaptureAsync(financial, recovered, scenario.Clock));
            Assert.Equal(2, await recovered.Users.CountAsync()); Assert.Equal(2, await recovered.HouseholdMembers.CountAsync());
            await RestoredControlSecurity.InvalidateHistoricalCredentialsAsync(recovered);
            await Assert.ThrowsAsync<HouseholdStorageAccessDeniedException>(() => storage.OpenForSessionAsync(principal, 1));
            Assert.Empty(await recovered.AuthRecoveryCodes.ToListAsync()); Assert.All(await recovered.AuthSessions.ToListAsync(), x => Assert.NotNull(x.RevokedAt));
            var user = await recovered.Users.SingleAsync(x => x.Id == "alice");
            Assert.NotEqual(originalUser.SecurityStamp, user.SecurityStamp);
            await recoveredGate.ResumeAsync(backupOperation);
            await using var reopened = await recoveredGate.EnterAsync();
            Assert.NotEqual(PasswordVerificationResult.Failed, new PasswordHasher<ApplicationUser>().VerifyHashedPassword(user, user.PasswordHash!, "Synthetic!2026"));
        } finally {
            if (target != null) {
                target.Database = "coiny_test_control";
                await using var admin = new NpgsqlConnection(target.ConnectionString); await admin.OpenAsync();
                await using var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS {targetName} WITH (FORCE)", admin); await drop.ExecuteNonQueryAsync();
            }
            await source.DisposeAsync(); Directory.Delete(artifacts, true); Directory.Delete(restoredDirectory, true); Directory.Delete(bundleDirectory, true);
        }
    }

    private static string TemporaryDirectory() { var path = Path.Combine(Path.GetTempPath(), "coiny-recovery-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(path); return path; }
    private sealed class RestoredFactory(string connection) : IDbContextFactory<ControlContext>
    {
        public ControlContext CreateDbContext() => new(new DbContextOptionsBuilder<ControlContext>().UseNpgsql(connection).Options);
    }
    private static async Task<string> OperatorTool(string tool, params string[] arguments)
    {
        var start = new ProcessStartInfo(tool) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEndAsync(); var errors = process.StandardError.ReadToEndAsync();
        try { await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(60)); }
        catch { process.Kill(entireProcessTree: true); throw; }
        await errors;
        Assert.True(process.ExitCode == 0, "Operator tool failed; private artifact paths and contents omitted.");
        return await output;
    }

    private static async Task PgTool(string tool, NpgsqlConnectionStringBuilder connection, params string[] arguments)
    {
        var start = new ProcessStartInfo(tool) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        start.Environment["PGPASSWORD"] = connection.Password;
        foreach (var item in new[] { "--host", connection.Host!, "--port", connection.Port.ToString(), "--username", connection.Username!, "--dbname", connection.Database! }.Concat(arguments)) start.ArgumentList.Add(item);
        using var process = Process.Start(start)!;
        var stdout = process.StandardOutput.ReadToEndAsync(); var stderr = process.StandardError.ReadToEndAsync();
        try { await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(60)); }
        catch { process.Kill(entireProcessTree: true); throw; }
        await stdout; await stderr;
        Assert.True(process.ExitCode == 0, tool + " failed; credentials and database contents are omitted from the test output.");
    }
}
