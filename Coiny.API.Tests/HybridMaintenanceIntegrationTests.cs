using Coiny.API.Data.Hybrid;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Coiny.API.Tests;

public class HybridMaintenanceIntegrationTests
{
    internal static async Task<InstallationMaintenance> GateAsync(HybridStorageIntegrationTests fixture)
    {
        await using var db = await fixture.factory.CreateDbContextAsync();
        return new InstallationMaintenance(Options.Create(new HybridStorageOptions { ControlConnection = db.Database.GetConnectionString()! }));
    }

    [PostgresFact]
    public async Task Pause_denies_new_work_and_drains_prior_central_and_household_work_across_instances()
    {
        await using var fixture = new HybridStorageIntegrationTests(); await fixture.InitializeAsync();
        var first = await GateAsync(fixture); var second = await GateAsync(fixture);
        var centralRequest = await first.EnterAsync(); var householdWork = await second.EnterAsync();
        var operation = Guid.NewGuid();
        await first.RequestAsync(operation);
        Assert.Equal(operation, await second.StatusAsync());
        await Assert.ThrowsAsync<InstallationMaintenanceException>(() => second.EnterAsync());
        // A pending drain must not finish while either admitted unit is alive.
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var drained = second.DrainAsync(operation, cancellation.Token);
        await centralRequest.DisposeAsync();
        Assert.False(drained.IsCompleted);
        await householdWork.DisposeAsync();
        await using (var exclusive = await drained) {
            await Assert.ThrowsAsync<InstallationMaintenanceException>(() => first.ResumeAsync(operation));
            await Assert.ThrowsAsync<InstallationMaintenanceException>(() => first.EnterAsync());
        }
        // Releasing the drain session does not implicitly clear the durable pause.
        await Assert.ThrowsAsync<InstallationMaintenanceException>(() => first.EnterAsync());
        await Assert.ThrowsAsync<InstallationMaintenanceException>(() => second.ResumeAsync(Guid.NewGuid()));
        await second.ResumeAsync(operation);
        await using var reopened = await first.EnterAsync(); Assert.Null(await second.StatusAsync());
    }

    [PostgresFact]
    public async Task Cancelled_drain_and_recreated_operator_leave_pause_in_place_until_matching_resume()
    {
        await using var fixture = new HybridStorageIntegrationTests(); await fixture.InitializeAsync();
        var gate = await GateAsync(fixture); var active = await gate.EnterAsync(); var operation = Guid.NewGuid();
        await gate.RequestAsync(operation);
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => gate.DrainAsync(operation, cancellation.Token));
        await active.DisposeAsync();
        var restarted = await GateAsync(fixture);
        Assert.Equal(operation, await restarted.StatusAsync());
        await Assert.ThrowsAsync<InstallationMaintenanceException>(() => restarted.RequestAsync(Guid.NewGuid()));
        await restarted.RequestAsync(operation); // Same operation can resume an interrupted drain.
        await using (var drained = await restarted.DrainAsync(operation)) { }
        await restarted.ResumeAsync(operation);
        await using var allowed = await gate.EnterAsync();
    }

    [PostgresFact]
    public async Task Missing_maintenance_row_fails_closed_instead_of_admitting_requests()
    {
        await using var fixture = new HybridStorageIntegrationTests(); await fixture.InitializeAsync();
        await using var db = await fixture.factory.CreateDbContextAsync();
        await db.Set<InstallationMaintenanceState>().ExecuteDeleteAsync();
        var gate = await GateAsync(fixture);
        await Assert.ThrowsAsync<InstallationMaintenanceException>(() => gate.EnterAsync());
        await Assert.ThrowsAsync<InstallationMaintenanceException>(() => gate.StatusAsync());
    }
}
