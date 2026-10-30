using System.Security.Claims;
using Coiny.API.Data.Hybrid;
using Coiny.API.Enums;
using Coiny.API.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Coiny.API.Tests;

public sealed class PostgresFactAttribute : FactAttribute
{
    public PostgresFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("COINY_TEST_POSTGRES")))
            Skip = "Requires the coiny-test Docker integration profile (real PostgreSQL).";
    }
}

// Every test owns a freshly migrated temporary PostgreSQL DB and synthetic SQLite
// root. The dedicated Docker role is required; never target a development DB.
public sealed class HybridStorageIntegrationTests : IAsyncLifetime
{
    private string adminConnection = "";
    private readonly string database = "coiny_it_" + Guid.NewGuid().ToString("N");
    private readonly string directory = Path.Combine(Path.GetTempPath(), "coiny-it-" + Guid.NewGuid().ToString("N"));
    private ServiceProvider services = null!;
    internal HybridStorageRoot root = null!;
    internal IDbContextFactory<ControlContext> factory = null!;
    internal HouseholdDatabaseService storage = null!;

    public async Task InitializeAsync()
    {
        // Skip discovery prevents execution without this explicit opt-in.
        var config = Environment.GetEnvironmentVariable("COINY_TEST_POSTGRES");
        if (config == null) return;
        var builder = new NpgsqlConnectionStringBuilder(config);
        if (builder.Host != "postgres" || builder.Database != "coiny_test_control" || builder.Username != "coiny_test")
            throw new InvalidOperationException("Integration tests require the isolated coiny-test PostgreSQL service.");
        builder.Pooling = false; adminConnection = builder.ToString();
        await using (var connection = new NpgsqlConnection(adminConnection)) {
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand($"CREATE DATABASE {database}", connection);
            await command.ExecuteNonQueryAsync();
        }
        builder.Database = database;
        var collection = new ServiceCollection();
        collection.AddLogging(); collection.AddDbContextFactory<ControlContext>(o => o.UseNpgsql(builder.ToString()));
        collection.AddIdentityCore<ApplicationUser>().AddRoles<IdentityRole>().AddEntityFrameworkStores<ControlContext>();
        services = collection.BuildServiceProvider(); factory = services.GetRequiredService<IDbContextFactory<ControlContext>>();
        await using (var db = await factory.CreateDbContextAsync()) await db.Database.MigrateAsync();
        root = new HybridStorageRoot(directory, "test-node");
        storage = new HouseholdDatabaseService(factory, root, TimeProvider.System);
    }

    public async Task DisposeAsync()
    {
        root?.Dispose(); if (services != null) await services.DisposeAsync();
        if (!string.IsNullOrEmpty(adminConnection)) {
            await using var connection = new NpgsqlConnection(adminConnection); await connection.OpenAsync();
            await using var command = new NpgsqlCommand($"DROP DATABASE IF EXISTS {database} WITH (FORCE)", connection);
            await command.ExecuteNonQueryAsync();
        }
        if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
    }

    internal async Task<(int Id, ClaimsPrincipal Principal, string UserId)> HouseholdAsync(string name)
    {
        using var scope = services.CreateScope(); var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = new ApplicationUser { UserName = name + "@coiny.test", Email = name + "@coiny.test" };
        Assert.True((await users.CreateAsync(user, "Synthetic!2026")).Succeeded);
        Assert.True(await users.CheckPasswordAsync(user, "Synthetic!2026"));
        await using var db = await factory.CreateDbContextAsync();
        var household = new Household { Name = name }; db.Add(household); await db.SaveChangesAsync();
        db.Add(new HouseholdMember { HouseholdId = household.Id, UserId = user.Id, Role = HouseholdRole.Owner });
        var session = new AuthSession { UserId = user.Id, CreatedAt = DateTime.UtcNow, ExpiresAt = DateTime.UtcNow.AddHours(1) };
        db.Add(session); await db.SaveChangesAsync();
        var principal = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, user.Id),
            new Claim("jti", session.Id), new Claim("security_stamp", user.SecurityStamp!) }, "test"));
        return (household.Id, principal, user.Id);
    }

    [PostgresFact]
    public async Task Real_provider_migrates_identity_and_keeps_financial_tables_out_of_PostgreSQL()
    {
        await HouseholdAsync("alice"); await using var db = await factory.CreateDbContextAsync();
        Assert.Single(await db.Users.ToListAsync());
        Assert.Empty(await db.Database.GetPendingMigrationsAsync()); Assert.False(db.Database.HasPendingModelChanges());
        Assert.Equal(4, (await db.Database.GetAppliedMigrationsAsync()).Count());
        var tables = await db.Database.SqlQueryRaw<string>("SELECT tablename AS \"Value\" FROM pg_tables WHERE schemaname='public'").ToListAsync();
        Assert.Contains("AspNetUsers", tables); Assert.Contains("AuthSessions", tables); Assert.Contains("HouseholdDatabases", tables);
        foreach (var table in StorageModelOwnership.Financial.Values) Assert.DoesNotContain(table, tables);
    }

    [PostgresFact]
    public async Task Two_households_use_real_financial_schema_and_session_membership_gates_file_access()
    {
        var a = await HouseholdAsync("alice"); var b = await HouseholdAsync("bob");
        await Task.WhenAll(storage.ProvisionAsync(a.Id), storage.ProvisionAsync(b.Id));
        await using (var db = await storage.OpenForSessionAsync(a.Principal, a.Id)) {
            db.Add(new Account { HouseholdId = a.Id, Name = "Synthetic A", OpeningBalance = 123.45m, OpeningBalanceDate = new(2026, 1, 1) });
            await db.SaveChangesAsync(); Assert.False(db.Database.HasPendingModelChanges());
            var tables = await db.Database.SqlQueryRaw<string>("SELECT name AS Value FROM sqlite_master WHERE type='table'").ToListAsync();
            Assert.DoesNotContain("AspNetUsers", tables); Assert.DoesNotContain("Households", tables);
        }
        await using (var db = await storage.OpenForSessionAsync(b.Principal, b.Id)) Assert.Empty(await db.Set<Account>().ToListAsync());
        var files = Directory.GetFiles(directory).Order().ToArray();
        await Assert.ThrowsAsync<HouseholdStorageAccessDeniedException>(() => storage.OpenForSessionAsync(a.Principal, b.Id));
        await Assert.ThrowsAsync<HouseholdStorageAccessDeniedException>(() => storage.OpenForSessionAsync(new ClaimsPrincipal(), a.Id));
        Assert.Equal(files, Directory.GetFiles(directory).Order().ToArray());
        await using (var db = await factory.CreateDbContextAsync()) {
            await db.Set<HouseholdMember>().Where(m => m.UserId == a.UserId).ExecuteDeleteAsync();
        }
        await Assert.ThrowsAsync<HouseholdStorageAccessDeniedException>(() => storage.OpenForSessionAsync(a.Principal, a.Id));
        await using (var db = await factory.CreateDbContextAsync()) {
            await db.Set<AuthSession>().Where(s => s.UserId == b.UserId).ExecuteUpdateAsync(s => s.SetProperty(x => x.RevokedAt, DateTime.UtcNow));
        }
        await Assert.ThrowsAsync<HouseholdStorageAccessDeniedException>(() => storage.OpenForSessionAsync(b.Principal, b.Id));
    }

    [PostgresFact]
    public async Task Provisioning_recovers_catalog_and_file_boundaries_and_repeated_calls_preserve_data()
    {
        var a = await HouseholdAsync("alice");
        await using (var db = await factory.CreateDbContextAsync()) {
            db.Add(new HouseholdDatabase { HouseholdId = a.Id, OwnerNode = root.Node }); await db.SaveChangesAsync();
        }
        await storage.RecoverProvisioningAsync();
        await using (var db = await storage.OpenForSessionAsync(a.Principal, a.Id)) {
            db.Add(new Account { HouseholdId = a.Id, Name = "Keep me", OpeningBalanceDate = new(2026, 1, 1) }); await db.SaveChangesAsync();
        }
        await using (var db = await factory.CreateDbContextAsync()) {
            var registry = await db.HouseholdDatabases.SingleAsync(); registry.State = HouseholdDatabaseState.Provisioning; registry.SchemaVersion = 0;
            await db.SaveChangesAsync();
        }
        await storage.RecoverProvisioningAsync(); await storage.ProvisionAsync(a.Id);
        await using (var db = await storage.OpenForSessionAsync(a.Principal, a.Id)) Assert.Equal("Keep me", (await db.Set<Account>().SingleAsync()).Name);
        Assert.Single(Directory.GetFiles(directory, "*.db"));
        Assert.Empty(Directory.GetFiles(directory, "*.provisioning"));
    }

    [PostgresFact]
    public async Task Missing_wrong_file_and_wrong_node_are_rejected_without_creating_replacement_data()
    {
        var a = await HouseholdAsync("alice"); await storage.ProvisionAsync(a.Id);
        HouseholdDatabase registry;
        await using (var db = await factory.CreateDbContextAsync()) registry = await db.HouseholdDatabases.AsNoTracking().SingleAsync();
        await using (var db = await storage.OpenForSessionAsync(a.Principal, a.Id)) {
            var metadata = await db.Set<HouseholdDatabaseMetadata>().SingleAsync(); metadata.HouseholdId = a.Id + 100; await db.SaveChangesAsync();
        }
        await Assert.ThrowsAsync<HouseholdStorageUnavailableException>(() => storage.OpenForSessionAsync(a.Principal, a.Id));
        var path = root.DatabasePath(registry.StorageKey); File.Move(path, path + ".hidden");
        await Assert.ThrowsAsync<Microsoft.Data.Sqlite.SqliteException>(() => storage.OpenForSessionAsync(a.Principal, a.Id));
        Assert.False(File.Exists(path)); File.Move(path + ".hidden", path);
        await using (var db = await factory.CreateDbContextAsync()) {
            await db.HouseholdDatabases.ExecuteUpdateAsync(s => s.SetProperty(x => x.OwnerNode, "another-node"));
        }
        await Assert.ThrowsAsync<HouseholdStorageUnavailableException>(() => storage.OpenForSessionAsync(a.Principal, a.Id));
        Assert.Throws<IOException>(() => new HybridStorageRoot(directory, "another-node"));
    }

    [PostgresFact]
    public async Task Preference_recovery_finishes_a_local_commit_without_incrementing_its_version_twice()
    {
        var a = await HouseholdAsync("alice"); await storage.ProvisionAsync(a.Id);
        await using var central = await factory.CreateDbContextAsync();
        var operation = new PendingPreferenceChange { UserId = a.UserId, HouseholdId = a.Id, IncludeMortgage = false, ScanEnabled = true, ExpectedVersion = 0 };
        central.Add(operation); await central.SaveChangesAsync();
        await using var local = await storage.OpenForSessionAsync(a.Principal, a.Id);
        local.Add(new HouseholdRecurringSetting { HouseholdId = a.Id, Enabled = true, EditVersion = 1 });
        local.Add(new PreferenceChangeReceipt { Id = operation.Id }); await local.SaveChangesAsync();
        var recovery = new HybridPreferenceChanges(central, local, TimeProvider.System);
        await recovery.CompleteAsync(operation.Id); await recovery.CompleteAsync(operation.Id);
        Assert.False((await central.Users.SingleAsync()).IncludeMortgageInNetWorth);
        Assert.Equal(1, (await local.Set<HouseholdRecurringSetting>().SingleAsync()).EditVersion);
        Assert.NotNull(operation.CompletedAt);
        await Assert.ThrowsAsync<Coiny.API.Services.BudgetConflictException>(() => recovery.SaveAsync(a.UserId, a.Id, true, false, 0));
        Assert.False((await central.Users.SingleAsync()).IncludeMortgageInNetWorth);
    }

    [PostgresFact]
    public async Task Unavailable_catalog_fails_before_financial_file_access()
    {
        var a = await HouseholdAsync("alice"); await storage.ProvisionAsync(a.Id);
        // Remove this disposable database's registry table; no other service/database is stopped.
        await using (var db = await factory.CreateDbContextAsync()) await db.Database.ExecuteSqlRawAsync("DROP TABLE \"HouseholdDatabases\"");
        var file = Directory.GetFiles(directory, "*.db").Single(); var before = File.GetLastWriteTimeUtc(file);
        await Assert.ThrowsAsync<PostgresException>(() => storage.OpenForSessionAsync(a.Principal, a.Id));
        Assert.Equal(before, File.GetLastWriteTimeUtc(file));
    }
}
