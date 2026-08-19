using ProjectWarehouse.Server.Domain;

namespace ProjectWarehouse.Server.Infrastructure.Realtime;

/// <summary>One person looking at an object, however many tabs they have open.</summary>
public sealed record RealtimeViewer(Guid UserId, string UserName);

/// <summary>
/// Presence over <see cref="EntityWatchRegistry"/>: the registry counts connections, this counts people.
/// Owns every mutation of the registry so the event is published exactly when the deduplicated list
/// changes — a second tab of the same person is not news to anybody.
/// </summary>
public class EntityPresenceService(
    EntityWatchRegistry watches,
    RealtimeConnectionManager connections,
    IRealtimeNotifier notifier)
{
    public IReadOnlyList<RealtimeViewer> GetViewers(AppEntityType entityType, Guid entityId) =>
        watches.GetWatchers(entityType, entityId)
            .Select(connections.Find)
            .OfType<RealtimeConnection>()
            .DistinctBy(c => c.UserId)
            .Select(c => new RealtimeViewer(c.UserId, c.UserName))
            .ToList();

    public async ValueTask WatchAsync(Guid connectionId, AppEntityType entityType, Guid entityId,
        CancellationToken ct = default)
    {
        var before = GetViewers(entityType, entityId);
        watches.Watch(connectionId, entityType, entityId);

        await PublishIfChangedAsync(entityType, entityId, before, ct);
    }

    public async ValueTask UnwatchAsync(Guid connectionId, AppEntityType entityType, Guid entityId,
        CancellationToken ct = default)
    {
        var before = GetViewers(entityType, entityId);
        watches.Unwatch(connectionId, entityType, entityId);

        await PublishIfChangedAsync(entityType, entityId, before, ct);
    }

    /// <summary>
    /// Call after the connection is gone from the manager — the departing stream must not be sent its own
    /// farewell. The owner's id is what tells a closed last tab from one of several.
    /// </summary>
    public async ValueTask RemoveConnectionAsync(Guid connectionId, Guid userId, CancellationToken ct = default)
    {
        foreach (var key in watches.RemoveConnection(connectionId))
        {
            var viewers = GetViewers(key.EntityType, key.EntityId);
            if (viewers.Any(v => v.UserId == userId)) continue;

            await PublishAsync(key.EntityType, key.EntityId, viewers, ct);
        }
    }

    private async ValueTask PublishIfChangedAsync(AppEntityType entityType, Guid entityId,
        IReadOnlyList<RealtimeViewer> before, CancellationToken ct)
    {
        var after = GetViewers(entityType, entityId);
        if (before.Select(v => v.UserId).ToHashSet().SetEquals(after.Select(v => v.UserId))) return;

        await PublishAsync(entityType, entityId, after, ct);
    }

    private ValueTask PublishAsync(AppEntityType entityType, Guid entityId, IReadOnlyList<RealtimeViewer> viewers,
        CancellationToken ct) =>
        notifier.PublishAsync(RealtimeAddress.ToWatchers(entityType, entityId), new RealtimeEvent
        {
            Payload = new EntityPresenceChangedPayload
            {
                EntityType = entityType,
                EntityId = entityId,
                Viewers = viewers,
            },
        }, ct);
}
