using System.Collections.Concurrent;
using System.Collections.Immutable;
using ProjectWarehouse.Server.Domain;

namespace ProjectWarehouse.Server.Infrastructure.Realtime;

/// <summary>
/// In-memory edit locks. A lock never outlives the connections that took it, so it needs no expiry of
/// its own — anything still in here belongs to a live stream. The reverse index mirrors
/// <see cref="EntityWatchRegistry"/>: a dropped stream must release its locks without walking them all.
/// </summary>
/// <remarks>
/// One object is held by one <em>user</em>, but by any number of their connections at once. A second tab
/// joins the holders instead of taking the lock over: with a takeover, neither tab could tell "someone
/// else has it" from "I have it elsewhere", and both would keep grabbing it back from each other.
/// </remarks>
public class EditLockStore
{
    private readonly ConcurrentDictionary<EntityWatchKey, Entry> _locks = new();
    private readonly ConcurrentDictionary<Guid, ImmutableHashSet<EntityWatchKey>> _byConnection = new();

    /// <summary>Value equality on <see cref="Lock"/>, reference equality on the set — exactly what CAS wants.</summary>
    private sealed record Entry(EditLock Lock, ImmutableHashSet<Guid> Connections);

    /// <summary>
    /// <see cref="Held"/> is false when the connection was not among the holders. <see cref="Emptied"/>
    /// is set only when the last holder left — the one transition worth announcing to watchers.
    /// </summary>
    public readonly record struct ReleaseResult(bool Held, EditLock? Emptied);

    /// <summary>
    /// Grants the lock, or returns the live one held by another user. <c>FirstHolder</c> marks the
    /// empty → held transition, which is what <c>editLockAcquired</c> announces; joining an object this
    /// user already holds elsewhere announces nothing, since their colleagues were told once already.
    /// </summary>
    public (EditLock Lock, bool Acquired, bool FirstHolder) Acquire(AppEntityType entityType, Guid entityId,
        Guid userId, string userName, Guid connectionId)
    {
        var key = new EntityWatchKey(entityType, entityId);

        while (true)
        {
            if (_locks.TryGetValue(key, out var existing))
            {
                if (existing.Lock.UserId != userId) return (existing.Lock, false, false);
                if (existing.Connections.Contains(connectionId)) return (existing.Lock, true, false);

                var joined = existing with { Connections = existing.Connections.Add(connectionId) };
                if (!_locks.TryUpdate(key, joined, existing)) continue;

                Attach(connectionId, key);
                return (joined.Lock, true, false);
            }

            var fresh = new Entry(new EditLock(entityType, entityId, userId, userName, DateTime.UtcNow),
                [connectionId]);
            if (!_locks.TryAdd(key, fresh)) continue;

            Attach(connectionId, key);
            return (fresh.Lock, true, true);
        }
    }

    /// <summary>
    /// Whether this connection still holds anything. The heartbeat answers with it: a tab that is editing
    /// keeps its subscriptions while backgrounded, and only it knows that from the reply.
    /// </summary>
    public bool Holds(Guid connectionId)
    {
        if (!_byConnection.TryGetValue(connectionId, out var keys)) return false;

        foreach (var key in keys)
            if (_locks.TryGetValue(key, out var existing) && existing.Connections.Contains(connectionId))
                return true;

        return false;
    }

    public ReleaseResult Release(AppEntityType entityType, Guid entityId, Guid connectionId) =>
        Release(new EntityWatchKey(entityType, entityId), connectionId);

    /// <summary>Releases everything a dropped stream held — the SSE equivalent of OnDisconnectedAsync.</summary>
    /// <returns>Only the objects left with no holder at all; the rest are still being edited elsewhere.</returns>
    public IReadOnlyCollection<EditLock> ReleaseByConnection(Guid connectionId)
    {
        var emptied = new List<EditLock>();

        // Looped, not a single TryRemove: an acquire that raced the teardown re-attaches to a connection
        // nobody will clean up again, and its key would sit in the reverse index forever.
        while (_byConnection.TryRemove(connectionId, out var keys))
        {
            foreach (var key in keys)
                if (Release(key, connectionId).Emptied is { } gone)
                    emptied.Add(gone);
        }

        return emptied;
    }

    private ReleaseResult Release(EntityWatchKey key, Guid connectionId)
    {
        while (_locks.TryGetValue(key, out var existing))
        {
            if (!existing.Connections.Contains(connectionId)) return new ReleaseResult(false, null);

            var remaining = existing.Connections.Remove(connectionId);
            if (remaining.IsEmpty)
            {
                if (!_locks.TryRemove(new KeyValuePair<EntityWatchKey, Entry>(key, existing))) continue;

                Detach(connectionId, key);
                return new ReleaseResult(true, existing.Lock);
            }

            if (!_locks.TryUpdate(key, existing with { Connections = remaining }, existing)) continue;

            Detach(connectionId, key);
            return new ReleaseResult(true, null);
        }

        return new ReleaseResult(false, null);
    }

    private void Attach(Guid connectionId, EntityWatchKey key) =>
        _byConnection.AddOrUpdate(connectionId, _ => [key], (_, set) => set.Add(key));

    private void Detach(Guid connectionId, EntityWatchKey key)
    {
        while (_byConnection.TryGetValue(connectionId, out var set))
        {
            var updated = set.Remove(key);
            if (ReferenceEquals(updated, set)) return;

            if (updated.IsEmpty)
            {
                if (_byConnection.TryRemove(new KeyValuePair<Guid, ImmutableHashSet<EntityWatchKey>>(connectionId, set)))
                    return;
            }
            else if (_byConnection.TryUpdate(connectionId, updated, set))
            {
                return;
            }
        }
    }
}
