using Coiny.API.Data;
using Coiny.API.Enums;
using Coiny.API.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Coiny.API.Tests;

public class BudgetMigrationTests
{
    private const string BeforeBudgets = "20261015141018_AddUserFirstName";
    private const string Foundation = "20261016182722_AddHouseholdBudgetFoundation";

    [Fact]
    public async Task SourceIdentityMigrationBackfillsDistinctKeysWithoutChangingExistingPaycheckData()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
        await using var context = new CoinyContext(new DbContextOptionsBuilder<CoinyContext>().UseSqlite(connection).Options);
        var migrator = context.GetService<IMigrator>();
        await migrator.MigrateAsync("20261016201255_AddPaycheckDetailsAndExtendedBudgetSchedules");
        context.Households.Add(new() { Id = 1, Name = "Existing budget" }); await context.SaveChangesAsync();
        await context.Database.ExecuteSqlRawAsync("""
            INSERT INTO HouseholdBudgets (Id, HouseholdId, CreatedAt) VALUES (1, 1, '2026-10-16');
            INSERT INTO BudgetRevisions (Id, HouseholdBudgetId, RevisionNumber, EditVersion, EffectiveFromMonth, State, SetupStep, CreatedAt)
                VALUES (1, 1, 1, 0, '2026-10-01', 0, 0, '2026-10-16');
            INSERT INTO BudgetIncomeSources (BudgetRevisionId, Name, Amount, HealthInsurancePerPaycheck, ScheduleFrequency, ScheduleStartDate, CreatedAt)
                VALUES (1, 'Salary', '1000', '123.45', 0, '2026-10-02', '2026-10-16'),
                (1, 'Salary', '2000', NULL, 0, '2026-10-09', '2026-10-16');
            """);
        await migrator.MigrateAsync();
        var sources = await context.BudgetIncomeSources.AsNoTracking().OrderBy(i => i.Id).ToListAsync();
        Assert.Equal(2, sources.Select(i => i.SourceKey).Distinct().Count());
        Assert.All(sources, i => Assert.NotEqual(Guid.Empty, i.SourceKey));
        Assert.Equal(123.45m, sources[0].HealthInsurancePerPaycheck);
        Assert.Equal(2000, sources[1].Amount);
        Assert.Empty(await context.Transactions.ToListAsync());
    }

    [Fact]
    public async Task BudgetMigrationsApplyRollbackAndReapplyWithoutChangingLedgerData()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = new CoinyContext(new DbContextOptionsBuilder<CoinyContext>().UseSqlite(connection).Options);
        var migrator = context.GetService<IMigrator>();
        await migrator.MigrateAsync(BeforeBudgets);
        context.Households.Add(new() { Id = 1, Name = "Existing household" });
        context.Accounts.Add(new() { Id = 1, HouseholdId = 1, Name = "Checking", AccountType = AccountType.Checking,
            OpeningBalance = 100, OpeningBalanceDate = new(2026,10,1) });
        context.Categories.Add(new() { Id = 1, HouseholdId = 1, Name = "Groceries", CategoryType = CategoryType.Expense, IsActive = true });
        await context.SaveChangesAsync();
        // Seed the historical schema without asking today's model to write newer columns.
        await context.Database.ExecuteSqlRawAsync("""
            INSERT INTO Transactions (AccountId, HouseholdId, CategoryId, Amount, TransactionDate, Description, IsCleared, CreatedAt)
            VALUES (1, 1, 1, '12.34', '2026-10-16', 'Existing groceries', 0, '2026-10-16');
            """);
        context.ChangeTracker.Clear();
        await migrator.MigrateAsync();
        Assert.False(context.Database.HasPendingModelChanges());
        Assert.Equal(12.34m, (await context.Transactions.SingleAsync()).Amount);
        await migrator.MigrateAsync(BeforeBudgets);
        Assert.Equal(100, (await context.Accounts.SingleAsync()).OpeningBalance);
        Assert.Equal(12.34m, await context.Database.SqlQueryRaw<decimal>("SELECT Amount AS Value FROM Transactions").SingleAsync());
        await migrator.MigrateAsync();
        Assert.Empty(await context.HouseholdBudgets.ToListAsync());
        Assert.Single(await context.Transactions.ToListAsync());
    }

    [Fact]
    public async Task EditVersionMigrationPreservesExistingPublishedBudgetAndOwnedSchedules()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = new CoinyContext(new DbContextOptionsBuilder<CoinyContext>().UseSqlite(connection).Options);
        var migrator = context.GetService<IMigrator>();
        await migrator.MigrateAsync(Foundation);
        context.Households.Add(new() { Id = 1, Name = "Existing budget home" });
        await context.SaveChangesAsync();
        await context.Database.ExecuteSqlRawAsync("""
            INSERT INTO HouseholdBudgets (Id, HouseholdId, CreatedAt) VALUES (1, 1, '2026-10-16');
            INSERT INTO BudgetRevisions (Id, HouseholdBudgetId, RevisionNumber, EffectiveFromMonth, State, SetupStep, PublishedAt, CreatedAt)
                VALUES (1, 1, 1, '2026-10-01', 1, 4, '2026-10-16', '2026-10-16');
            INSERT INTO BudgetIncomeSources (BudgetRevisionId, Name, Amount, ScheduleFrequency, ScheduleStartDate, ScheduleDayOfMonth, CreatedAt)
                VALUES (1, 'Salary', '5000.15', 3, '2026-10-01', 15, '2026-10-16');
            INSERT INTO BudgetAllocations (BudgetRevisionId, Name, Amount, AllocationType, CreatedAt)
                VALUES (1, 'Groceries', '500.25', 2, '2026-10-16');
            """);
        await migrator.MigrateAsync();
        var revision = await context.BudgetRevisions.AsNoTracking().Include(r => r.IncomeSources).Include(r => r.Allocations).AsSingleQuery().SingleAsync();
        Assert.Equal(0, revision.EditVersion);
        Assert.Equal(BudgetRevisionState.Published, revision.State);
        Assert.Equal(5000.15m, Assert.Single(revision.IncomeSources).Amount);
        Assert.Equal(15, Assert.Single(revision.IncomeSources).Schedule.DayOfMonth);
        Assert.Null(Assert.Single(revision.Allocations).Schedule);
        await migrator.MigrateAsync(Foundation);
        Assert.Equal(1, await context.Database.SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM BudgetRevisions").SingleAsync());
        await migrator.MigrateAsync();
        Assert.Equal(500.25m, (await context.BudgetAllocations.AsNoTracking().SingleAsync()).Amount);
    }
}
