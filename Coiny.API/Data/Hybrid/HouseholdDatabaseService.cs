using System.Collections.Concurrent;
using System.Security.Claims;
using Coiny.API.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Coiny.API.Data.Hybrid;

// Requests and jobs receive an already-open authorized household context.
// Clients never supply a filesystem path.
public sealed class HouseholdDatabaseService(
    IDbContextFactory<ControlContext> controls, HybridStorageRoot root, TimeProvider clock)
{
    public const int CurrentSchema = 2;
    public string Node => root.Node;
    private readonly ConcurrentDictionary<int, SemaphoreSlim> provisioning = new();

    private HouseholdContext Context(string path, SqliteOpenMode mode) => new(new DbContextOptionsBuilder<HouseholdContext>()
        .UseSqlite(new SqliteConnectionStringBuilder { DataSource = path, Mode = mode, Pooling = false,
            ForeignKeys = true, DefaultTimeout = 5 }.ToString()).Options);

    private static async Task VerifyAsync(HouseholdContext db, HouseholdDatabase registry, CancellationToken ct)
    {
        var metadata = await db.Set<HouseholdDatabaseMetadata>().AsNoTracking().SingleOrDefaultAsync(ct);
        if (metadata == null || metadata.HouseholdId != registry.HouseholdId || metadata.StorageKey != registry.StorageKey ||
            metadata.SchemaVersion != CurrentSchema) throw new HouseholdStorageUnavailableException();
    }

    public async Task<HouseholdContext> OpenForSessionAsync(ClaimsPrincipal principal, int householdId, CancellationToken ct = default)
    {
        var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        var sessionId = principal.FindFirstValue("jti");
        var stamp = principal.FindFirstValue("security_stamp");
        if (principal.Identity?.IsAuthenticated != true || string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(sessionId) || string.IsNullOrEmpty(stamp))
            throw new HouseholdStorageAccessDeniedException();
        await using var central = await controls.CreateDbContextAsync(ct);
        var now = clock.GetUtcNow().UtcDateTime;
        var valid = await central.Set<AuthSession>().AsNoTracking().AnyAsync(s => s.Id == sessionId && s.UserId == userId &&
            s.RevokedAt == null && s.ExpiresAt > now && s.User.SecurityStamp == stamp, ct);
        if (!valid || !await central.Set<HouseholdMember>().AsNoTracking().AnyAsync(m => m.UserId == userId && m.HouseholdId == householdId, ct))
            throw new HouseholdStorageAccessDeniedException();
        var registry = await central.HouseholdDatabases.AsNoTracking().SingleOrDefaultAsync(x => x.HouseholdId == householdId, ct);
        if (registry == null || registry.State != HouseholdDatabaseState.Ready || registry.OwnerNode != root.Node || registry.SchemaVersion != CurrentSchema)
            throw new HouseholdStorageUnavailableException();
        // Mode=ReadWrite forbids creating a blank file when registered storage is missing.
        var db = Context(root.DatabasePath(registry.StorageKey), SqliteOpenMode.ReadWrite);
        try {
            await db.Database.OpenConnectionAsync(ct);
            await VerifyAsync(db, registry, ct);
            return db;
        } catch {
            await db.DisposeAsync();
            throw;
        }
    }

    // Trusted background entry point, not exposed as an HTTP operation.
    internal async Task<HouseholdContext> OpenForWorkerAsync(int householdId, CancellationToken ct = default)
    {
        await using var central = await controls.CreateDbContextAsync(ct);
        var registry = await central.HouseholdDatabases.AsNoTracking().SingleOrDefaultAsync(x => x.HouseholdId == householdId, ct);
        if (registry == null || registry.State != HouseholdDatabaseState.Ready || registry.OwnerNode != root.Node || registry.SchemaVersion != CurrentSchema)
            throw new HouseholdStorageUnavailableException();
        var db = Context(root.DatabasePath(registry.StorageKey), SqliteOpenMode.ReadWrite);
        try { await db.Database.OpenConnectionAsync(ct); await VerifyAsync(db, registry, ct); return db; }
        catch { await db.DisposeAsync(); throw; }
    }

    internal async Task<IDisposable> AcquireWriterAsync(int householdId, CancellationToken ct)
    {
        var gate = provisioning.GetOrAdd(householdId, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(ct); return new WriterLease(gate);
    }
    private sealed class WriterLease(SemaphoreSlim gate) : IDisposable
    {
        public void Dispose() => gate.Release();
    }

    public async Task ProvisionAsync(int householdId, CancellationToken ct = default)
    {
        var gate = provisioning.GetOrAdd(householdId, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(ct);
        try {
            await using var central = await controls.CreateDbContextAsync(ct);
            if (!await central.Set<Household>().AnyAsync(h => h.Id == householdId, ct)) throw new HouseholdStorageUnavailableException();
            var registry = await central.HouseholdDatabases.SingleOrDefaultAsync(x => x.HouseholdId == householdId, ct);
            if (registry == null) {
                registry = new() { HouseholdId = householdId, OwnerNode = root.Node, State = HouseholdDatabaseState.Provisioning };
                central.Add(registry); await central.SaveChangesAsync(ct);
            }
            if (registry.OwnerNode != root.Node || registry.State == HouseholdDatabaseState.Maintenance || registry.SchemaVersion > CurrentSchema)
                throw new HouseholdStorageUnavailableException();
            var path = root.DatabasePath(registry.StorageKey);
            if (registry.State == HouseholdDatabaseState.Ready) {
                await using var ready = Context(path, SqliteOpenMode.ReadWrite);
                await ready.Database.OpenConnectionAsync(ct); await VerifyAsync(ready, registry, ct); return;
            }
            if (!File.Exists(path)) {
                // Unpublished staging files are recoverable. They cannot be opened by
                // a request, and the final file is published only after identity commit.
                var staging = path + ".provisioning";
                await using (var db = Context(staging, SqliteOpenMode.ReadWriteCreate)) {
                    await db.Database.MigrateAsync(ct);
                    var metadata = await db.Set<HouseholdDatabaseMetadata>().SingleOrDefaultAsync(ct);
                    if (metadata == null) {
                        // A staging file without identity must still be empty financially.
                        if (await db.Set<Account>().AnyAsync(ct) || await db.Set<Transaction>().AnyAsync(ct)) throw new HouseholdStorageUnavailableException();
                        db.Add(new HouseholdDatabaseMetadata { HouseholdId = householdId, StorageKey = registry.StorageKey, SchemaVersion = CurrentSchema });
                        await db.SaveChangesAsync(ct);
                    }
                    await VerifyAsync(db, registry, ct);
                    await db.Database.ExecuteSqlRawAsync("PRAGMA wal_checkpoint(TRUNCATE)", ct);
                }
                File.Move(staging, path, overwrite: false);
            }
            await using (var db = Context(path, SqliteOpenMode.ReadWrite)) {
                await db.Database.OpenConnectionAsync(ct); await VerifyAsync(db, registry, ct);
                await db.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL", ct);
            }
            registry.SchemaVersion = CurrentSchema; registry.State = HouseholdDatabaseState.Ready;
            await central.SaveChangesAsync(ct);
        } finally { gate.Release(); }
    }

    public async Task RecoverProvisioningAsync(CancellationToken ct = default)
    {
        await using var central = await controls.CreateDbContextAsync(ct);
        var ids = await central.HouseholdDatabases.AsNoTracking().Where(x => x.OwnerNode == root.Node && x.State == HouseholdDatabaseState.Provisioning)
            .Select(x => x.HouseholdId).ToArrayAsync(ct);
        foreach (var id in ids) await ProvisionAsync(id, ct);
    }
}
