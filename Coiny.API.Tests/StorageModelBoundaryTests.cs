using Coiny.API.Data;
using Coiny.API.Data.Hybrid;
using Coiny.API.Models;
using Microsoft.EntityFrameworkCore;

namespace Coiny.API.Tests;

public class StorageModelBoundaryTests
{
    private static CoinyContext Legacy() => new(new DbContextOptionsBuilder<CoinyContext>().UseSqlite("Data Source=:memory:").Options);
    private static ControlContext Control() => new(new DbContextOptionsBuilder<ControlContext>().UseSqlite("Data Source=:memory:").Options);
    private static HouseholdContext Financial() => new(new DbContextOptionsBuilder<HouseholdContext>().UseSqlite("Data Source=:memory:").Options);

    [Fact]
    public void Every_legacy_entity_has_exactly_one_owner_including_owned_schedule_types()
    {
        using var legacy = Legacy(); using var control = Control(); using var financial = Financial();
        var central = control.Model.GetEntityTypes().Select(e => e.ClrType).ToHashSet();
        var household = financial.Model.GetEntityTypes().Select(e => e.ClrType).ToHashSet();
        Assert.Empty(central.Intersect(household));
        foreach (var entity in legacy.Model.GetEntityTypes())
            Assert.True(central.Contains(entity.ClrType) ^ household.Contains(entity.ClrType), entity.Name);
        Assert.Equal(new[] { typeof(HouseholdDatabase), typeof(HouseholdDatabaseMetadata), typeof(PendingPreferenceChange), typeof(PreferenceChangeReceipt), typeof(LegacyImportState), typeof(InstallationMaintenanceState) }.OrderBy(t => t.Name),
            central.Union(household).Except(legacy.Model.GetEntityTypes().Select(e => e.ClrType)).OrderBy(t => t.Name));
        Assert.DoesNotContain(typeof(ApplicationUser), household);
        Assert.DoesNotContain(typeof(Household), household);
        Assert.Contains(typeof(BudgetSchedule), household);
    }

    [Fact]
    public void Split_preserves_tables_columns_and_local_relationships_without_cross_store_foreign_keys()
    {
        using var legacy = Legacy(); using var control = Control(); using var financial = Financial();
        foreach (var model in new[] { control.Model, financial.Model })
        foreach (var entity in model.GetEntityTypes())
        {
            var original = legacy.Model.FindEntityType(entity.Name);
            if (original == null) continue; // New orchestration/identity metadata, checked separately.
            Assert.Equal(original.GetTableName(), entity.GetTableName());
            Assert.Equal(original.GetProperties().Select(p => p.Name).Order(), entity.GetProperties().Select(p => p.Name).Order());
            foreach (var fk in entity.GetForeignKeys()) Assert.Contains(fk.PrincipalEntityType, model.GetEntityTypes());
            var expected = original.GetForeignKeys().Where(f => model.FindEntityType(f.PrincipalEntityType.Name) != null)
                .Select(f => string.Join(",", f.Properties.Select(p => p.Name))).Order();
            Assert.Equal(expected, entity.GetForeignKeys().Select(f => string.Join(",", f.Properties.Select(p => p.Name))).Order());
        }
        Assert.NotNull(financial.Model.FindEntityType(typeof(HouseholdAlertRead))!.FindProperty("UserId"));
        Assert.Null(financial.Model.FindEntityType(typeof(HouseholdAlertRead))!.FindNavigation("User"));
    }

    [Fact]
    public async Task Financial_schema_can_save_without_identity_or_household_tables_and_still_invalidates_reconciliation()
    {
        await using var db = Financial();
        await db.Database.OpenConnectionAsync(); await db.Database.EnsureCreatedAsync();
        var account = new Account { HouseholdId = 42, Name = "Synthetic", OpeningBalanceDate = new(2026, 1, 1) };
        db.Add(account); await db.SaveChangesAsync();
        var checkpoint = new AccountReconciliation { HouseholdId = 42, AccountId = account.Id,
            StatementDate = new(2026, 1, 31), LedgerFingerprint = new string('a', 64),
            ConfirmedByUserId = "central-user", ConfirmedByName = "Tester" };
        db.Add(checkpoint); await db.SaveChangesAsync();
        account.OpeningBalance = 50; await db.SaveChangesAsync();
        Assert.NotNull(checkpoint.InvalidatedAt);
        await db.Entry(checkpoint).ReloadAsync(); Assert.NotNull(checkpoint.InvalidatedAt);
    }

    [Fact]
    public async Task Migration_verification_accepts_legacy_numeric_affinity_but_rejects_a_changed_decimal_amount()
    {
        await using var connection = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using (var setup = connection.CreateCommand()) {
            setup.CommandText = "CREATE TABLE old_amounts(Amount decimal(18,2)); CREATE TABLE new_amounts(Amount TEXT); INSERT INTO old_amounts VALUES (1234.56); INSERT INTO new_amounts SELECT Amount FROM old_amounts;";
            await setup.ExecuteNonQueryAsync();
        }
        await using var tx = connection.BeginTransaction();
        var decimals = new HashSet<string> { "Amount" };
        var oldRows = await LegacyTestMigration.RowsAsync(connection, "SELECT Amount FROM old_amounts", 0, tx, decimals);
        Assert.Equal(oldRows, await LegacyTestMigration.RowsAsync(connection, "SELECT Amount FROM new_amounts", 0, tx, decimals));
        await using (var change = connection.CreateCommand()) { change.Transaction = tx; change.CommandText = "UPDATE new_amounts SET Amount='1234.57'"; await change.ExecuteNonQueryAsync(); }
        Assert.NotEqual(oldRows, await LegacyTestMigration.RowsAsync(connection, "SELECT Amount FROM new_amounts", 0, tx, decimals));
    }
}
