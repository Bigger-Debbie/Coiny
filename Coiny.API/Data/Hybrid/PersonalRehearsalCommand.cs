using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Coiny.API.Interfaces;
using Coiny.API.Models;
using Coiny.API.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Coiny.API.Data.Hybrid;

// Offline read verification. Private reports contain only counts and component
// digests, never financial values, user identifiers or provider diagnostics.
internal static class PersonalRehearsalCommand
{
    private sealed class Clock(DateOnly date) : TimeProvider { public override DateTimeOffset GetUtcNow() => new(date.ToDateTime(new TimeOnly(16, 0)), TimeSpan.Zero); }
    private sealed class User(string id, int household) : ICurrentUserService {
        public string? GetUserId() => id;
        public bool IsAuthenticated() => true;
        public Task<int> GetCurrentHouseholdIdAsync() => Task.FromResult(household);
    }
    internal static async Task<Dictionary<string, string>> CaptureAsync(IFinancialContext financial, IControlContext control, string userId, int household, DateOnly date)
    {
        var user = new User(userId, household); var clock = new Clock(date);
        var budgets = new BudgetService(financial, user, new BudgetScheduleService(), clock);
        var actuals = new BudgetActualsService(financial, user, budgets, clock);
        var accounts = await new AccountService(financial, user, new AccountBalanceService(financial), new AccountBalanceSnapshotService(financial, user)).GetAccountsAsync();
        var components = new Dictionary<string, object?> { ["AccountsAndBalances"] = accounts };
        for (var offset = -1; offset <= 1; offset++) {
            var day = date.AddMonths(offset);
            components[$"MonthlyPlan{offset}"] = await budgets.GetMonthAsync(new(day.Year, day.Month, 1));
            var period = await budgets.GetIncomePeriodAsync(day);
            components[$"IncomePeriod{offset}"] = period;
            if (period.Plan != null && period.StartDate != null && period.EndDate != null)
                components[$"Actuals{offset}"] = await actuals.GetPeriodAsync(day, "all");
            else if (offset == 0) throw new InvalidOperationException("Current published income period is required for rehearsal.");
            else components[$"Actuals{offset}"] = null;
        }
        components["Forecast3Months"] = await actuals.GetForecastAsync(date, 3);
        components["AvailableCash"] = await actuals.GetAvailableCashAsync(new() { AsOf = date });
        components["SavingsFunds"] = await new SavingsFundsService(financial, user, budgets, clock).GetAsync();
        foreach (var account in accounts) components[$"Reconciliation{account.Id}"] = await new AccountReconciliationService(financial, user, clock, control).HistoryAsync(account.Id);
        return components.ToDictionary(x => x.Key, x => Hash(Canonical(JsonSerializer.SerializeToNode(x.Value))));
    }
    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private static string Canonical(JsonNode? node)
    {
        if (node is JsonObject obj) return "{" + string.Join(",", obj.OrderBy(x => x.Key, StringComparer.Ordinal).Select(x => JsonSerializer.Serialize(x.Key) + ":" + Canonical(x.Value))) + "}";
        if (node is JsonArray array) return "[" + string.Join(",", array.Select(Canonical)) + "]";
        // PostgreSQL timestamps have microsecond precision. Compare at that
        // precision, matching the migration engine, rather than changing history.
        if (node is JsonValue value && value.TryGetValue<string>(out var text) && text.Contains('T') && DateTimeOffset.TryParse(text, out var date))
            return JsonSerializer.Serialize(new DateTimeOffset(date.UtcTicks / 10 * 10, TimeSpan.Zero).ToString("O"));
        if (node is JsonValue number && number.TryGetValue<decimal>(out var amount))
            return amount.ToString("G29", System.Globalization.CultureInfo.InvariantCulture);
        return node?.ToJsonString() ?? "null";
    }
    internal static async Task<int> RunAsync(string[] args)
    {
        try {
            var builder = Host.CreateApplicationBuilder(Array.Empty<string>());
            builder.Services.AddHybridStorageFoundation(builder.Configuration);
            using var host = builder.Build();
            if (args is ["--recovery-security"]) {
                _ = host.Services.GetRequiredService<HybridStorageRoot>();
                await using var scope = host.Services.CreateAsyncScope();
                await RestoredControlSecurity.InvalidateHistoricalCredentialsAsync(scope.ServiceProvider.GetRequiredService<ControlContext>());
                Console.WriteLine("Historical sessions/codes invalidated and security stamps rotated; password hashes preserved."); return 0;
            }
            if (args is ["--restore-household-backups", var archiveDirectory] && Path.IsPathFullyQualified(archiveDirectory)) {
                await using var scope = host.Services.CreateAsyncScope();
                var control = scope.ServiceProvider.GetRequiredService<ControlContext>();
                var registry = await control.HouseholdDatabases.AsNoTracking().ToListAsync();
                var manifests = new List<HouseholdBackupManifest>();
                foreach (var path in Directory.GetFiles(archiveDirectory, "*.sqlite.json"))
                    manifests.Add(JsonSerializer.Deserialize<HouseholdBackupManifest>(await File.ReadAllTextAsync(path)) ?? throw new InvalidOperationException("Invalid household manifest."));
                if (manifests.Count != registry.Count || manifests.Select(x => x.HouseholdId).Distinct().Count() != registry.Count)
                    throw new InvalidOperationException("Backup household inventory does not match the registry.");
                var root = host.Services.GetRequiredService<HybridStorageRoot>();
                foreach (var manifest in manifests)
                    await HouseholdBackupArchive.RestoreToEmptyStorageAsync(archiveDirectory, manifest, root, registry.Single(x => x.HouseholdId == manifest.HouseholdId));
                Console.WriteLine("Household backups restored into empty storage; no activation performed."); return 0;
            }
            if (args is ["--household-backups", var directory] && Path.IsPathFullyQualified(directory)) {
                await using var scope = host.Services.CreateAsyncScope();
                var control = scope.ServiceProvider.GetRequiredService<ControlContext>();
                var storage = host.Services.GetRequiredService<HouseholdDatabaseService>();
                foreach (var id in await control.HouseholdDatabases.Select(x => x.HouseholdId).ToArrayAsync())
                    await HouseholdBackupArchive.CreateAsync(storage, id, directory);
                Console.WriteLine("Offline household backups created. Keep API/jobs stopped through the central dump."); return 0;
            }
            if (args is not ["--verify-household", var snapshot, var asOf, var report] || !DateOnly.TryParseExact(asOf, "yyyy-MM-dd", out var date) || !Path.IsPathFullyQualified(report))
                throw new ArgumentException("Expected snapshot, fixed date and private report path.");
            var plan = await LegacyMigrationCommand.PlanAsync(snapshot);
            if (plan.Households != 1) throw new InvalidOperationException("Verification requires a selected single-household snapshot.");
            await using var legacy = new CoinyContext(new DbContextOptionsBuilder<CoinyContext>().UseSqlite(new SqliteConnectionStringBuilder { DataSource = snapshot, Mode = SqliteOpenMode.ReadOnly, Pooling = false }.ToString()).Options);
            var household = await legacy.Households.Select(x => x.Id).SingleAsync();
            var user = await legacy.HouseholdMembers.OrderBy(x => x.Id).Select(x => x.UserId).FirstAsync();
            await using var controlScope = host.Services.CreateAsyncScope();
            var central = controlScope.ServiceProvider.GetRequiredService<ControlContext>();
            if (await central.Households.CountAsync() != 1 || await central.Users.CountAsync() != plan.Users) throw new InvalidOperationException("Destination scope differs from selection.");
            var storageService = host.Services.GetRequiredService<HouseholdDatabaseService>();
            await using var financial = await storageService.OpenForWorkerAsync(household);
            var before = await CaptureAsync(legacy, legacy, user, household, date);
            var after = await CaptureAsync(financial, central, user, household, date);
            var checks = before.ToDictionary(x => x.Key, x => after.GetValueOrDefault(x.Key) == x.Value);
            var valid = checks.Values.All(x => x) && plan.SourceSha256 == await HouseholdBackupArchive.DigestAsync(snapshot);
            var options = new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write, Share = FileShare.None };
            if (!OperatingSystem.IsWindows()) options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
            await using var output = new FileStream(report, options);
            await JsonSerializer.SerializeAsync(output, new { Format = 1, Valid = valid, AsOf = date, plan.SourceSha256, Checks = checks, Before = before, After = after });
            Console.WriteLine(valid ? $"PASS: {checks.Count} financial output groups match the selected snapshot." : "Financial output comparison failed. Inspect the private component report; do not activate.");
            return valid ? 0 : 1;
        } catch (Exception error) {
            Console.Error.WriteLine($"Private rehearsal failed ({error.GetType().Name}); financial values and credentials omitted. No activation performed."); return 1;
        }
    }
}
