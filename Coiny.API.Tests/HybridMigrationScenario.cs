using System.Text.Json;
using Coiny.API.Data;
using Coiny.API.Data.Hybrid;
using Coiny.API.DTOs.Savings;
using Coiny.API.Enums;
using Coiny.API.Interfaces;
using Coiny.API.Models;
using Coiny.API.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Coiny.API.Tests;

internal sealed class HybridMigrationScenario : IAsyncDisposable
{
    private BudgetTestDatabase original = null!;
    public TestClock Clock => original.Clock;
    private readonly string snapshot = Path.Combine(Path.GetTempPath(), "coiny-migration-" + Guid.NewGuid().ToString("N") + ".db");
    public CoinyContext Source { get; private set; } = null!;
    public string PathName => snapshot;
    public string Sha { get; private set; } = "";
    public string Expected { get; private set; } = "";
    internal sealed class User(int household = 1) : ICurrentUserService
    {
        public string? GetUserId() => household == 1 ? "alice" : "bob";
        public bool IsAuthenticated() => true;
        public Task<int> GetCurrentHouseholdIdAsync() => Task.FromResult(household);
    }
    public static async Task<HybridMigrationScenario> CreateAsync()
    {
        var scenario = new HybridMigrationScenario(); scenario.original = await BudgetTestDatabase.CreateAsync();
        var db = scenario.original; var c = db.Context();
        foreach (var id in new[] { "alice", "bob" }) {
            var user = new ApplicationUser { Id = id, UserName = id + "@coiny.test", Email = id + "@coiny.test", NormalizedUserName = (id + "@coiny.test").ToUpperInvariant(), NormalizedEmail = (id + "@coiny.test").ToUpperInvariant(), SecurityStamp = Guid.NewGuid().ToString() };
            user.PasswordHash = new PasswordHasher<ApplicationUser>().HashPassword(user, "Synthetic!2026"); c.Users.Add(user);
        }
        c.HouseholdMembers.AddRange(new HouseholdMember { HouseholdId = 1, UserId = "alice", Role = HouseholdRole.Owner }, new HouseholdMember { HouseholdId = 2, UserId = "bob", Role = HouseholdRole.Owner });
        c.Accounts.AddRange(new Account { Id = 1, HouseholdId = 1, Name = "Checking", AccountType = AccountType.Checking, OpeningBalance = 1000.12m, OpeningBalanceDate = new(2026, 1, 1) },
            new Account { Id = 2, HouseholdId = 1, Name = "Savings", AccountType = AccountType.Savings, OpeningBalance = 20000, OpeningBalanceDate = new(2026, 1, 1) },
            new Account { Id = 3, HouseholdId = 2, Name = "Other checking", OpeningBalance = 5000, OpeningBalanceDate = new(2026, 1, 1) });
        c.Categories.AddRange(new Category { Id = 1, HouseholdId = 1, Name = "Expense", CategoryType = CategoryType.Expense, IsActive = true },
            new Category { Id = 2, HouseholdId = 1, Name = "Income", CategoryType = CategoryType.Income, IsActive = true },
            new Category { Id = 3, HouseholdId = 1, Name = "Transfer", CategoryType = CategoryType.Transfer, IsSystem = true, IsActive = true },
            new Category { Id = 4, HouseholdId = 2, Name = "Other expense", CategoryType = CategoryType.Expense, IsActive = true });
        await c.SaveChangesAsync();
        var budgets = db.Service(); var draft = await budgets.StartDraftAsync();
        var saved = await budgets.SaveDraftAsync(draft.Id, new() { ExpectedEditVersion = draft.EditVersion, EffectiveFromMonth = new(2026, 10, 1), SetupStep = BudgetSetupStep.Review,
            IncomeSources = [new() { Name = "Pay", Amount = 2000, Schedule = new() { Frequency = BudgetFrequency.EveryTwoWeeks, StartDate = new(2026, 10, 2) } }],
            Allocations = [new() { Name = "Electric", Amount = 500, AllocationType = BudgetAllocationType.Bills, Schedule = new() { Frequency = BudgetFrequency.Monthly, StartDate = new(2026, 10, 1), DayOfMonth = 15 } },
                new() { Name = "Food", Amount = 300, AllocationType = BudgetAllocationType.Spending }, new() { Name = "Car", Amount = 500, AllocationType = BudgetAllocationType.Savings },
                new() { Name = "Pets", Amount = 100, AllocationType = BudgetAllocationType.Savings } ] });
        await budgets.PublishDraftAsync(saved.Id, saved.EditVersion);
        var keys = saved.Allocations.ToDictionary(x => x.Name, x => x.SourceKey!.Value);
        c.Set<TransactionImportBatch>().Add(new TransactionImportBatch { Id = 1, HouseholdId = 1, AccountId = 1, CreatedByUserId = "alice", Filename = "synthetic.csv", SubmissionId = Guid.NewGuid(), PayloadHash = new string('a', 64), ImportedCount = 2 });
        c.Transactions.AddRange(new Transaction { Id = 1, HouseholdId = 1, AccountId = 1, CategoryId = 1, Amount = 150.25m, Description = "Utility first", TransactionDate = new(2026, 10, 15), ImportBatchId = 1, ImportRowId = 1 },
            new Transaction { Id = 2, HouseholdId = 1, AccountId = 1, CategoryId = 1, Amount = 175.50m, Description = "Utility second", TransactionDate = new(2026, 10, 15), ImportBatchId = 1, ImportRowId = 2 },
            new Transaction { Id = 3, HouseholdId = 1, AccountId = 1, CategoryId = 2, Amount = 2000, Description = "Paycheck", TransactionDate = new(2026, 10, 2) },
            new Transaction { Id = 4, HouseholdId = 1, AccountId = 1, CategoryId = 1, Amount = 35.75m, Description = "Food", TransactionDate = new(2026, 10, 10) },
            new Transaction { Id = 5, HouseholdId = 2, AccountId = 3, CategoryId = 4, Amount = 99.99m, Description = "Other household", TransactionDate = new(2026, 10, 10) });
        await c.SaveChangesAsync(); await ImportBaseline.Capture(c, new[] { 1, 2 }); await c.SaveChangesAsync();
        await new BillPaymentService(db.Context(), new User(), budgets, db.Clock).SaveAsync(keys["Electric"], new(2026, 10, 15), new() { IsPaid = true, TransactionIds = [1, 2] });
        var latest = await budgets.GetManagementAsync();
        var component = new SavingsComponent { Name = "Tires", Mode = "Monthly", Amount = 70 };
        var target = new SavingsComponent { Name = "Tags", Mode = "Recurring", Amount = 200, TargetDate = new(2027, 5, 15), RepeatMonths = 12 };
        var fundContext = db.Context();
        var fundBudgets = new BudgetService(fundContext, new User(), new BudgetScheduleService(), db.Clock);
        await new SavingsFundsService(fundContext, new User(), fundBudgets, db.Clock).SaveAsync(new SaveSavingsFund { SourceKey = keys["Car"], Name = "Car", AccountId = 2, ExpectedRevisionId = latest.Revision.Id,
            AcceptShortfall = true, Plan = new() { StartDate = new(2026, 10, 16), Components = [component, target] }, OpeningAllocations = new() { [component.Key] = 100, [target.Key] = 50 } });
        c.BillPayments.Add(new BillPayment { HouseholdId = 1, AllocationKey = keys["Pets"], DueDate = new(2026, 10, 29), IsSkipped = true, SkippedPeriodStart = new(2026, 10, 16), SkippedPeriodEnd = new(2026, 10, 29), Note = "Existing savings" });
        c.BudgetCategoryRules.Add(new BudgetCategoryRule { HouseholdId = 1, CategoryId = 1, AllocationKey = keys["Food"], EffectiveFromMonth = new(2026, 10, 1) });
        c.SavedCsvMappings.Add(new SavedCsvMapping { HouseholdId = 1, Name = "Synthetic", NormalizedName = "SYNTHETIC", SettingsJson = "{}" });
        c.MerchantCategoryRules.Add(new MerchantCategoryRule { HouseholdId = 1, CategoryId = 1, MatchType = "Contains", Pattern = "Market", NormalizedPattern = "MARKET" });
        c.HouseholdRecurringSettings.Add(new HouseholdRecurringSetting { HouseholdId = 1, Enabled = true, LastScanDate = new(2026, 10, 16) });
        var alert = new HouseholdAlert { HouseholdId = 1, PatternKey = "synthetic", Merchant = "Synthetic recurring", Frequency = "Monthly", TypicalAmount = 10, NextExpected = new(2026, 11, 1), UpdatedAt = new(2026, 10, 16, 0, 0, 0, DateTimeKind.Utc) }; c.Add(alert); await c.SaveChangesAsync();
        c.HouseholdAlertReads.Add(new HouseholdAlertRead { AlertId = alert.Id, UserId = "alice", Version = 1 }); await c.SaveChangesAsync();
        var transferContext = db.Context();
        await new TransferService(transferContext, new User(), new AccountBalanceSnapshotService(transferContext, new User()))
            .CreateAsync(new() { FromAccountId = 1, ToAccountId = 2, Amount = 25, Date = new(2026, 10, 16), CreateBothEntries = true });
        var reconcile = new AccountReconciliationService(db.Context(), new User(), db.Clock);
        var check = await reconcile.CheckAsync(1, new() { StatementDate = new(2026, 10, 16), StatementBalance = 2613.62m });
        await reconcile.ConfirmAsync(1, new() { StatementDate = check.StatementDate, StatementBalance = check.StatementBalance, TransactionsReviewed = true, ReviewedFingerprint = check.LedgerFingerprint });
        await c.Database.ExecuteSqlRawAsync("UPDATE sqlite_sequence SET seq=10000 WHERE name='Transactions'");
        scenario.Expected = await CaptureAsync(db.Context(), db.Context(), db.Clock);
        await using (var destination = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = scenario.snapshot, Pooling = false }.ToString())) {
            await destination.OpenAsync(); ((SqliteConnection)c.Database.GetDbConnection()).BackupDatabase(destination);
        }
        scenario.Source = new CoinyContext(new DbContextOptionsBuilder<CoinyContext>().UseSqlite(new SqliteConnectionStringBuilder { DataSource = scenario.snapshot, Mode = SqliteOpenMode.ReadOnly, Pooling = false }.ToString()).Options);
        scenario.Sha = await HouseholdBackupArchive.DigestAsync(scenario.snapshot); return scenario;
    }

    public static async Task<string> CaptureAsync(IFinancialContext financial, IControlContext control, TimeProvider clock)
    {
        var user = new User(); var budgets = new BudgetService(financial, user, new BudgetScheduleService(), clock);
        var actuals = new BudgetActualsService(financial, user, budgets, clock);
        return JsonSerializer.Serialize(new {
            Month = await budgets.GetMonthAsync(new(2026, 10, 1)), Period = await budgets.GetIncomePeriodAsync(new(2026, 10, 16)),
            Actuals = await actuals.GetPeriodAsync(new(2026, 10, 16), "all"), Forecast = await actuals.GetForecastAsync(new(2026, 10, 16), 3),
            Cash = await actuals.GetAvailableCashAsync(new() { AsOf = new(2026, 10, 16), AccountIds = [1, 2] }),
            Funds = await new SavingsFundsService(financial, user, budgets, clock).GetAsync(),
            Reconciliation = await new AccountReconciliationService(financial, user, clock, control).HistoryAsync(1)
        });
    }
    public async ValueTask DisposeAsync()
    {
        if (Source != null) await Source.DisposeAsync(); if (original != null) await original.DisposeAsync();
        if (File.Exists(snapshot)) File.Delete(snapshot);
    }
}
