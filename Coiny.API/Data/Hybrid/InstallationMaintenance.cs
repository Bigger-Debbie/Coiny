using Microsoft.Extensions.Options;
using Npgsql;

namespace Coiny.API.Data.Hybrid;

public sealed class InstallationMaintenanceState
{
    public int Id { get; set; } = 1;
    public Guid? OperationId { get; set; }
    public DateTime? RequestedAt { get; set; }
}

public sealed class InstallationMaintenanceException() : InvalidOperationException("Installation maintenance is active.");

// Cooperative admission/drain protocol, not distributed fencing against a
// partitioned SQLite owner. Operators must stop API/jobs before backup/restore.
public sealed class InstallationMaintenance(IOptions<HybridStorageOptions> options)
{
    private const long LockKey = 0x434F494E594D4149; // COINYMAI; stable across nodes/releases.
    private string ConnectionString => new NpgsqlConnectionStringBuilder(options.Value.ControlConnection) {
        Pooling = false, Timeout = 10, CommandTimeout = 15
    }.ToString(); // Closing a lease must close its PostgreSQL session and locks.

    private async Task<NpgsqlConnection> ConnectAsync(CancellationToken cancellation)
    {
        var connection = new NpgsqlConnection(ConnectionString);
        try { await connection.OpenAsync(cancellation); return connection; }
        catch { await connection.DisposeAsync(); throw; }
    }

    public async Task<IAsyncDisposable> EnterAsync(CancellationToken cancellation = default)
    {
        var connection = await ConnectAsync(cancellation);
        try {
            await using var command = new NpgsqlCommand($"SELECT pg_try_advisory_lock_shared({LockKey})", connection);
            if (!(bool)(await command.ExecuteScalarAsync(cancellation))!) throw new InstallationMaintenanceException();
            command.CommandText = "SELECT \"OperationId\" IS NULL FROM \"InstallationMaintenance\" WHERE \"Id\"=1";
            if (await command.ExecuteScalarAsync(cancellation) is not true) throw new InstallationMaintenanceException();
            return connection;
        } catch { await connection.DisposeAsync(); throw; }
    }

    public async Task RequestAsync(Guid operation, CancellationToken cancellation = default)
    {
        if (operation == Guid.Empty) throw new ArgumentException("A nonempty operation ID is required.");
        await using var connection = await ConnectAsync(cancellation);
        await using var command = new NpgsqlCommand("UPDATE \"InstallationMaintenance\" SET \"OperationId\"=@id, \"RequestedAt\"=COALESCE(\"RequestedAt\",CURRENT_TIMESTAMP) WHERE \"Id\"=1 AND (\"OperationId\" IS NULL OR \"OperationId\"=@id)", connection);
        command.Parameters.AddWithValue("id", operation);
        if (await command.ExecuteNonQueryAsync(cancellation) != 1) throw new InstallationMaintenanceException();
    }

    public async Task<IAsyncDisposable> DrainAsync(Guid operation, CancellationToken cancellation = default)
    {
        var connection = await ConnectAsync(cancellation);
        try {
            await using var command = new NpgsqlCommand($"SELECT pg_advisory_lock({LockKey})", connection) { CommandTimeout = 60 };
            await command.ExecuteNonQueryAsync(cancellation);
            command.CommandText = "SELECT \"OperationId\" FROM \"InstallationMaintenance\" WHERE \"Id\"=1";
            if (await command.ExecuteScalarAsync(cancellation) is not Guid current || current != operation) throw new InstallationMaintenanceException();
            return connection;
        } catch { await connection.DisposeAsync(); throw; }
    }

    public async Task ResumeAsync(Guid operation, CancellationToken cancellation = default)
    {
        await using var connection = await ConnectAsync(cancellation);
        await using var command = new NpgsqlCommand($"SELECT pg_try_advisory_lock({LockKey})", connection);
        if (!(bool)(await command.ExecuteScalarAsync(cancellation))!) throw new InstallationMaintenanceException();
        command.CommandText = "UPDATE \"InstallationMaintenance\" SET \"OperationId\"=NULL, \"RequestedAt\"=NULL WHERE \"Id\"=1 AND \"OperationId\"=@id";
        command.Parameters.AddWithValue("id", operation);
        if (await command.ExecuteNonQueryAsync(cancellation) != 1) throw new InstallationMaintenanceException();
    }

    public async Task<Guid?> StatusAsync(CancellationToken cancellation = default)
    {
        await using var connection = await ConnectAsync(cancellation);
        await using var command = new NpgsqlCommand("SELECT \"OperationId\" FROM \"InstallationMaintenance\" WHERE \"Id\"=1", connection);
        var value = await command.ExecuteScalarAsync(cancellation);
        if (value is null) throw new InstallationMaintenanceException();
        return value is Guid id ? id : null;
    }
}
