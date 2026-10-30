using System.Reflection;
using System.Text.Json;
using Coiny.API.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Coiny.API.Data.Hybrid;

internal sealed record MigrationCheckpoint(string Phase, int? HouseholdId);

// Shared offline engine; only the restricted Docker CLI is exposed so far.
internal static class LegacyMigrationEngine
{
    internal static async Task ImportAsync(CoinyContext legacy, ControlContext control,
        HouseholdDatabaseService storage, string snapshot, string sha,
        Func<MigrationCheckpoint, Task>? checkpoint = null)
    {
        if (Path.GetFullPath(snapshot) != Path.GetFullPath(legacy.Database.GetDbConnection().DataSource) || await HouseholdBackupArchive.DigestAsync(snapshot) != sha)
            throw new InvalidOperationException("The snapshot path or digest does not match the migration source.");
        var existing = await control.Set<LegacyImportState>().SingleOrDefaultAsync();
        if (existing != null && existing.SourceSha256 != sha) throw new InvalidOperationException("The migration source changed; refusing to mix snapshots.");
        if (existing?.CompletedAt != null) return;
        await ValidateSnapshotAsync(legacy);
        var integrity = await legacy.Database.SqlQueryRaw<string>("PRAGMA integrity_check").ToListAsync();
        if (integrity.Count != 1 || integrity[0] != "ok") throw new InvalidOperationException("Source integrity check failed.");
        if (existing == null) {
            if (await control.Users.AnyAsync() || await control.Set<Household>().AnyAsync()) throw new InvalidOperationException("Control destination is not empty.");
            await using var transaction = await control.Database.BeginTransactionAsync();
            var types = legacy.Model.GetEntityTypes().Where(e => !e.IsOwned() && control.Model.FindEntityType(e.ClrType) != null).ToList();
            var inserted = new HashSet<Type>();
            while (types.Count > 0) {
                var next = types.FirstOrDefault(e => e.GetForeignKeys().All(f => f.PrincipalEntityType == e || inserted.Contains(f.PrincipalEntityType.ClrType)))
                    ?? throw new InvalidOperationException("Unsupported control relationship cycle.");
                var rows = await ReadAsync(legacy, next.ClrType);
                foreach (var row in rows) {
                    foreach (var property in next.GetProperties()) {
                        var value = property.PropertyInfo?.GetValue(row);
                        if (value is DateTime date) property.PropertyInfo!.SetValue(row, DateTime.SpecifyKind(date, DateTimeKind.Utc));
                        if (value is DateTimeOffset offset) property.PropertyInfo!.SetValue(row, offset.ToUniversalTime());
                    }
                    // Every browser signs in again after the storage transition.
                    if (row is AuthSession session) session.RevokedAt = DateTime.UtcNow;
                }
                control.AddRange(rows); await control.SaveChangesAsync();
                if (next.FindPrimaryKey()?.Properties is { Count: 1 } key && key[0].ClrType == typeof(int)) {
                    var table = Quote(next.GetTableName()!); var column = Quote(key[0].GetColumnName());
                    // Identifiers come only from our EF model; SQL string arguments are parameterized.
                    var tableName = next.GetTableName()!;
                    var sourceSequence = await legacy.Database.SqlQuery<long>($"SELECT COALESCE(MAX(seq),0) AS Value FROM sqlite_sequence WHERE name={tableName}").SingleAsync();
                    var sequenceSql = $"SELECT setval(pg_get_serial_sequence({{0}}, {{1}}), GREATEST(COALESCE((SELECT MAX({column}) FROM {table}),0)+1, {{2}}+1), false)";
                    await control.Database.ExecuteSqlRawAsync(sequenceSql, table, key[0].GetColumnName(), sourceSequence);
                }
                inserted.Add(next.ClrType); types.Remove(next);
            }
            existing = new LegacyImportState { SourceSha256 = sha }; control.Add(existing); await control.SaveChangesAsync(); await transaction.CommitAsync();
            if (checkpoint != null) await checkpoint(new("ControlCommitted", null));
        }
        var households = await control.Set<Household>().AsNoTracking().Select(x => x.Id).ToArrayAsync();
        var copiedCounts = StorageModelOwnership.Financial.Values.ToDictionary(x => x, _ => 0L);
        foreach (var id in households) {
            await storage.ProvisionAsync(id);
            await using var financial = await storage.OpenForWorkerAsync(id);
            var connection = (SqliteConnection)financial.Database.GetDbConnection();
            await using (var attach = connection.CreateCommand()) {
                attach.CommandText = "ATTACH DATABASE $source AS legacy"; attach.Parameters.AddWithValue("$source", snapshot); await attach.ExecuteNonQueryAsync();
            }
            await using var transaction = await connection.BeginTransactionAsync();
            await using (var defer = connection.CreateCommand()) { defer.Transaction = (SqliteTransaction)transaction; defer.CommandText = "PRAGMA defer_foreign_keys=ON"; await defer.ExecuteNonQueryAsync(); }
            foreach (var table in StorageModelOwnership.Financial.Values) {
                var columns = await ColumnsAsync(connection, table, (SqliteTransaction)transaction);
                var list = string.Join(",", columns.Select(Quote));
                var filter = HouseholdFilter(table);
                var decimalColumns = financial.Model.GetEntityTypes().Where(x => x.GetTableName() == table)
                    .SelectMany(x => x.GetProperties()).Where(x => (Nullable.GetUnderlyingType(x.ClrType) ?? x.ClrType) == typeof(decimal))
                    .Select(x => x.GetColumnName()).ToHashSet();
                await using var copy = connection.CreateCommand(); copy.Transaction = (SqliteTransaction)transaction;
                copy.CommandText = $"INSERT OR IGNORE INTO {Quote(table)} ({list}) SELECT {list} FROM legacy.{Quote(table)} WHERE {filter}";
                copy.Parameters.AddWithValue("$household", id); await copy.ExecuteNonQueryAsync();
                var expected = await RowsAsync(connection, $"SELECT {list} FROM legacy.{Quote(table)} WHERE {filter}", id, (SqliteTransaction)transaction, decimalColumns);
                var actual = await RowsAsync(connection, $"SELECT {list} FROM {Quote(table)}", id, (SqliteTransaction)transaction, decimalColumns);
                if (!expected.SequenceEqual(actual)) {
                    var differences = new List<string>();
                    foreach (var column in columns) {
                        var beforeColumn = await RowsAsync(connection, $"SELECT {Quote(column)} FROM legacy.{Quote(table)} WHERE {filter}", id, (SqliteTransaction)transaction);
                        var afterColumn = await RowsAsync(connection, $"SELECT {Quote(column)} FROM {Quote(table)}", id, (SqliteTransaction)transaction);
                        if (!beforeColumn.SequenceEqual(afterColumn)) differences.Add(column);
                    }
                    throw new InvalidOperationException($"Financial verification failed for {table} (expected {expected.Count} rows, found {actual.Count}; differing columns: {string.Join(",", differences)}); source is unchanged.");
                }
                copiedCounts[table] += actual.Count;
                // Preserve AUTOINCREMENT high-water marks, including IDs of deleted
                // rows referenced by import/audit history.
                await using var sequence = connection.CreateCommand(); sequence.Transaction = (SqliteTransaction)transaction;
                sequence.CommandText = "UPDATE sqlite_sequence SET seq=MAX(seq, COALESCE((SELECT seq FROM legacy.sqlite_sequence WHERE name=$table),0)) WHERE name=$table; " +
                    "INSERT INTO sqlite_sequence(name,seq) SELECT name,seq FROM legacy.sqlite_sequence WHERE name=$table AND NOT EXISTS(SELECT 1 FROM sqlite_sequence WHERE name=$table)";
                sequence.Parameters.AddWithValue("$table", table); await sequence.ExecuteNonQueryAsync();
            }
            await transaction.CommitAsync();
            if (checkpoint != null) await checkpoint(new("HouseholdCommitted", id));
        }
        foreach (var (table, count) in copiedCounts) {
            // Table identifiers are selected exclusively from the fixed financial inventory.
            var countSql = $"SELECT COUNT(*) AS Value FROM {Quote(table)}";
            var sourceCount = await legacy.Database.SqlQueryRaw<long>(countSql).SingleAsync();
            if (sourceCount != count) throw new InvalidOperationException($"Unassigned or cross-household rows found in {table}.");
        }
        foreach (var type in legacy.Model.GetEntityTypes().Where(e => !e.IsOwned() && control.Model.FindEntityType(e.ClrType) != null)) {
            var before = await ReadAsync(legacy, type.ClrType); var after = await ReadAsync(control, type.ClrType);
            if (!Canonical(type, before).SequenceEqual(Canonical(type, after))) throw new InvalidOperationException($"Control verification failed for {type.GetTableName()}.");
        }
        if (await HouseholdBackupArchive.DigestAsync(snapshot) != sha) throw new InvalidOperationException("The snapshot changed during migration; activation was refused.");
        if (checkpoint != null) await checkpoint(new("BeforeActivation", null));
        existing.CompletedAt = DateTime.UtcNow; await control.SaveChangesAsync();
    }

    internal static async Task ValidateSnapshotAsync(CoinyContext source)
    {
        if ((await source.Database.GetPendingMigrationsAsync()).Any()) throw new InvalidOperationException("The source has unapplied migrations.");
        var path = source.Database.GetDbConnection().DataSource;
        if (!File.Exists(path)) throw new InvalidOperationException("A consistent file snapshot is required.");
        await using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path, Mode = SqliteOpenMode.ReadOnly, Pooling = false }.ToString());
        await connection.OpenAsync();
        await using (var check = connection.CreateCommand()) {
            check.CommandText = "PRAGMA foreign_key_check";
            await using var reader = await check.ExecuteReaderAsync();
            if (await reader.ReadAsync()) throw new InvalidOperationException("Source foreign-key check failed.");
        }
        await using (var attach = connection.CreateCommand()) {
            attach.CommandText = "ATTACH DATABASE $source AS legacy"; attach.Parameters.AddWithValue("$source", path); await attach.ExecuteNonQueryAsync();
        }
        // A valid SQLite FK does not itself guarantee that related rows belong to
        // the same household. Reject such references before writing either store.
        foreach (var entity in source.Model.GetEntityTypes().Where(x => !x.IsOwned() && StorageModelOwnership.Financial.ContainsKey(x.ClrType))) {
            foreach (var fk in entity.GetForeignKeys().Where(x => StorageModelOwnership.Financial.ContainsKey(x.PrincipalEntityType.ClrType))) {
                var table = entity.GetTableName()!; var parent = fk.PrincipalEntityType.GetTableName()!;
                var join = string.Join(" AND ", fk.Properties.Zip(fk.PrincipalKey.Properties).Select(x => $"c.{Quote(x.First.GetColumnName())}=p.{Quote(x.Second.GetColumnName())}"));
                await using var check = connection.CreateCommand();
                check.CommandText = $"SELECT COUNT(*) FROM legacy.{Quote(table)} c JOIN legacy.{Quote(parent)} p ON {join} WHERE {Owner(table, "c")} <> {Owner(parent, "p")}";
                if (Convert.ToInt64(await check.ExecuteScalarAsync()) != 0) throw new InvalidOperationException($"Cross-household relationship found in {table}.");
            }
        }
    }

    private static string Owner(string table, string alias) => table switch {
        "AccountBalanceSnapshots" => $"(SELECT HouseholdId FROM legacy.Accounts WHERE Id={alias}.AccountId)",
        "BillPaymentTransaction" => $"(SELECT HouseholdId FROM legacy.BillPayments WHERE Id={alias}.BillPaymentId)",
        "BudgetRevisions" => $"(SELECT HouseholdId FROM legacy.HouseholdBudgets WHERE Id={alias}.HouseholdBudgetId)",
        "BudgetAllocations" or "BudgetIncomeSources" => $"(SELECT b.HouseholdId FROM legacy.BudgetRevisions r JOIN legacy.HouseholdBudgets b ON b.Id=r.HouseholdBudgetId WHERE r.Id={alias}.BudgetRevisionId)",
        "SavingsActivity" => $"(SELECT HouseholdId FROM legacy.SavingsFund WHERE Id={alias}.SavingsFundId)",
        "HouseholdAlertReads" => $"(SELECT HouseholdId FROM legacy.HouseholdAlerts WHERE Id={alias}.AlertId)",
        _ => $"{alias}.HouseholdId"
    };

    private static string Quote(string name) => "\"" + name.Replace("\"", "\"\"") + "\"";
    private static Task<List<object>> ReadAsync(DbContext db, Type type) => (Task<List<object>>)typeof(LegacyMigrationEngine)
        .GetMethod(nameof(ReadTypedAsync), BindingFlags.NonPublic | BindingFlags.Static)!.MakeGenericMethod(type).Invoke(null, new object[] { db })!;
    private static async Task<List<object>> ReadTypedAsync<T>(DbContext db) where T : class => (await db.Set<T>().AsNoTracking().ToListAsync()).Cast<object>().ToList();
    private static List<string> Canonical(IEntityType type, List<object> rows) => rows.Select(row => JsonSerializer.Serialize(type.GetProperties()
        .Where(p => !(row is AuthSession && p.Name == nameof(AuthSession.RevokedAt))).OrderBy(p => p.Name).ToDictionary(p => p.Name, p => {
            var value = p.PropertyInfo!.GetValue(row);
            return value is DateTime date ? (object)(date.Ticks / 10) : value is DateTimeOffset offset ? offset.UtcTicks / 10 : value;
        }))).Order(StringComparer.Ordinal).ToList();
    private static async Task<List<string>> ColumnsAsync(SqliteConnection connection, string table, SqliteTransaction tx)
    {
        await using var cmd = connection.CreateCommand(); cmd.Transaction = tx; cmd.CommandText = $"PRAGMA table_info({Quote(table)})";
        await using var reader = await cmd.ExecuteReaderAsync(); var result = new List<string>(); while (await reader.ReadAsync()) result.Add(reader.GetString(1)); return result;
    }
    internal static async Task<List<string>> RowsAsync(SqliteConnection connection, string sql, int household, SqliteTransaction tx, IReadOnlySet<string>? decimalColumns = null)
    {
        await using var cmd = connection.CreateCommand(); cmd.Transaction = tx; cmd.CommandText = sql; cmd.Parameters.AddWithValue("$household", household);
        await using var reader = await cmd.ExecuteReaderAsync(); var rows = new List<string>();
        while (await reader.ReadAsync()) {
            var values = new object?[reader.FieldCount];
            for (var i = 0; i < reader.FieldCount; i++) {
                // Old SQLite migrations used NUMERIC affinity for some decimal
                // columns; the current model uses TEXT. Compare via the same
                // decimal reader EF uses, never floats or display formatting.
                values[i] = reader.IsDBNull(i) ? null : decimalColumns?.Contains(reader.GetName(i)) == true
                    ? reader.GetDecimal(i).ToString("G29", System.Globalization.CultureInfo.InvariantCulture) : reader.GetValue(i);
            }
            rows.Add(JsonSerializer.Serialize(values));
        }
        return rows.Order(StringComparer.Ordinal).ToList();
    }
    private static string HouseholdFilter(string table) => table switch {
        "AccountBalanceSnapshots" => "AccountId IN (SELECT Id FROM legacy.Accounts WHERE HouseholdId=$household)",
        "BillPaymentTransaction" => "BillPaymentId IN (SELECT Id FROM legacy.BillPayments WHERE HouseholdId=$household)",
        "BudgetRevisions" => "HouseholdBudgetId IN (SELECT Id FROM legacy.HouseholdBudgets WHERE HouseholdId=$household)",
        "BudgetAllocations" or "BudgetIncomeSources" => "BudgetRevisionId IN (SELECT Id FROM legacy.BudgetRevisions WHERE HouseholdBudgetId IN (SELECT Id FROM legacy.HouseholdBudgets WHERE HouseholdId=$household))",
        "SavingsActivity" => "SavingsFundId IN (SELECT Id FROM legacy.SavingsFund WHERE HouseholdId=$household)",
        "HouseholdAlertReads" => "AlertId IN (SELECT Id FROM legacy.HouseholdAlerts WHERE HouseholdId=$household)",
        _ => "HouseholdId=$household"
    };
}
