namespace ProjectWarehouse.Server.Infrastructure.Realtime;

/// <summary>
/// Holds the events a request publishes while its transaction is open. A watcher told about a change
/// before the commit refetches the old state and is never told again, so the events wait for the commit
/// and are dropped on rollback.
/// </summary>
public sealed class RealtimeOutbox
{
    private static readonly object ItemsKey = new();

    private readonly List<(RealtimeAddress Address, RealtimeEvent Event)> _pending = [];

    public static RealtimeOutbox? Current(HttpContext? context) =>
        context?.Items.TryGetValue(ItemsKey, out var value) == true ? value as RealtimeOutbox : null;

    public static RealtimeOutbox Open(HttpContext context)
    {
        var outbox = new RealtimeOutbox();
        context.Items[ItemsKey] = outbox;
        return outbox;
    }

    public void Add(RealtimeAddress address, RealtimeEvent evt) => _pending.Add((address, evt));

    /// <summary>Detaches the outbox before publishing, so the notifier sends instead of buffering again.</summary>
    public async Task FlushAsync(HttpContext context, IRealtimeNotifier notifier, CancellationToken ct)
    {
        context.Items.Remove(ItemsKey);
        foreach (var (address, evt) in _pending)
            await notifier.PublishAsync(address, evt, ct);
        _pending.Clear();
    }

    public void Discard(HttpContext context)
    {
        context.Items.Remove(ItemsKey);
        _pending.Clear();
    }
}
