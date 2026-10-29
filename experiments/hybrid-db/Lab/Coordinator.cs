using System.Collections.Concurrent;
namespace HybridLab;
// One owning application process in this prototype. Not a distributed lock/lease.
public class Coordinator
{
    public class Slot
    {
        public readonly object Sync = new();
        public readonly SemaphoreSlim Writer = new(1, 1);
        public int Active;
        public bool Maintenance;
        public TaskCompletionSource Drained = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
    private readonly ConcurrentDictionary<Guid, Slot> slots = new();
    public ConcurrentDictionary<Guid, bool> Holds { get; } = new();
    public Slot Get(Guid id) => slots.GetOrAdd(id, _ => new());
    public IDisposable Enter(Guid id)
    {
        var slot = Get(id);
        lock (slot.Sync) {
            if (slot.Maintenance) throw new LabFault(503, "Household maintenance is in progress.");
            if (slot.Active++ == 0) slot.Drained = new(TaskCreationOptions.RunContinuationsAsynchronously);
        }
        return new ActionLease(() => { lock (slot.Sync) { if (--slot.Active == 0) slot.Drained.TrySetResult(); } });
    }
    public async Task<IDisposable> Maintain(Guid id)
    {
        var slot = Get(id); Task wait;
        lock (slot.Sync) {
            if (slot.Maintenance) throw new LabFault(409, "Maintenance already running.");
            slot.Maintenance = true;
            wait = slot.Active == 0 ? Task.CompletedTask : slot.Drained.Task;
        }
        await wait;
        return new ActionLease(() => { lock (slot.Sync) slot.Maintenance = false; });
    }
    private sealed class ActionLease(Action release) : IDisposable { public void Dispose() => release(); }
}
