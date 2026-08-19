using System.Collections.Concurrent;
using System.Collections.Immutable;
using ProjectWarehouse.Server.Domain;

namespace ProjectWarehouse.Server.Infrastructure.Realtime;

/// <summary>
/// In-memory edit locks. A lock lives tens of seconds and never outlives the process, so persisting it
/// would only buy a steady stream of heartbeat writes. The reverse index mirrors
/// <see cref="EntityWatchRegistry"/>: a dropped stream must release its locks without walking them all.
/// </summary>
public class EditLockStore
{
    public static readonly TimeSpan Ttl = TimeSpan.FromSeconds(60);

    private readonly ConcurrentDictionary<EntityWatchKey, EditLock> _locks = new();
    private readonly ConcurrentDictionary<Guid, ImmutableHashSet<EntityWatchKey>> _byConnection = new();

    /// <summary>
    /// Grants the lock, or returns the live one held by someone else. The same user coming from another
    /// connection takes it over: a person competing with their own second tab is not a conflict.
    /// </summary>
    public (EditLock Lock, bool Acquired) Acquire(AppEntityType entityType, Guid entityId, Guid userId,
        string userName, Guid connectionId)
    {
        var key = new EntityWatchKey(entityType, entityId);

        while (true)
        {
            var now = DateTime.UtcNow;
            var fresh = new EditLock(entityType, entityId, userId, userName, connectionId, now, now + Ttl);

            if (_locks.TryGetValue(key, out var existing))
            {
                if (existing.ExpiresAt > now && existing.UserId != userId) return (existing, false);

                fresh = fresh with { AcquiredAt = existing.UserId == userId ? existing.AcquiredAt : now };
                if (!_locks.TryUpdate(key, fresh, existing)) continue;

                if (existing.ConnectionId != connectionId) Detach(existing.ConnectionId, key);
                Attach(connectionId, key);
                return (fresh, true);
            }

            if (!_locks.TryAdd(key, fresh)) continue;

            Attach(connectionId, key);
            return (fresh, true);
        }
    }

    /// <summary>Extends a lock held by this very connection. Anything else is "not held".</summary>
    public EditLock? Heartbeat(AppEntityType entityType, Guid entityId, Guid connectionId)
    {
        var key = new EntityWatchKey(entityType, entityId);

        while (_locks.TryGetValue(key, out var existing))
        {
            var now = DateTime.UtcNow;
            if (existing.ConnectionId != connectionId || existing.ExpiresAt <= now) return null;

            var extended = existing with { ExpiresAt = now + Ttl };
            if (_locks.TryUpdate(key, extended, existing)) return extended;
        }

        return null;
    }

    public EditLock? Release(AppEntityType entityType, Guid entityId, Guid connectionId)
    {
        var key = new EntityWatchKey(entityType, entityId);

        while (_locks.TryGetValue(key, out var existing))
        {
            if (existing.ConnectionId != connectionId) return null;

            if (!_locks.TryRemove(new KeyValuePair<EntityWatchKey, EditLock>(key, existing))) continue;

            Detach(connectionId, key);
            return existing;
        }

        return null;
    }

    /// <summary>Releases everything a dropped stream held — the SSE equivalent of OnDisconnectedAsync.</summary>
    public IReadOnlyCollection<EditLock> ReleaseByConnection(Guid connectionId)
    {
        var released = new List<EditLock>();

        // Looped, not a single TryRemove: an acquire that raced the teardown re-attaches to a connection
        // nobody will clean up again, and its key would sit in the reverse index forever.
        while (_byConnection.TryRemove(connectionId, out var keys))
        {
            foreach (var key in keys)
            {
                if (!_locks.TryGetValue(key, out var existing) || existing.ConnectionId != connectionId) continue;
                if (!_locks.TryRemove(new KeyValuePair<EntityWatchKey, EditLock>(key, existing))) continue;

                released.Add(existing);
            }
        }

        return released;
    }

    /// <summary>
    /// Drops locks past their TTL. Without a sweep a tab killed by a dead battery would leave someone
    /// else's banner up until they reload the page — expiry alone raises no event.
    /// </summary>
    public IReadOnlyCollection<EditLock> SweepExpired()
    {
        var now = DateTime.UtcNow;
        var expired = new List<EditLock>();

        foreach (var (key, existing) in _locks)
        {
            if (existing.ExpiresAt > now) continue;
            if (!_locks.TryRemove(new KeyValuePair<EntityWatchKey, EditLock>(key, existing))) continue;

            Detach(existing.ConnectionId, key);
            expired.Add(existing);
        }

        return expired;
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
