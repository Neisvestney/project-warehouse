using System.Collections.Concurrent;
using System.Collections.Immutable;
using ProjectWarehouse.Server.Domain;

namespace ProjectWarehouse.Server.Infrastructure.Realtime;

public readonly record struct EntityWatchKey(AppEntityType EntityType, Guid EntityId);

/// <summary>
/// Who is currently looking at which object. The reverse index exists so that dropping a connection
/// does not have to walk every watched object.
/// </summary>
public class EntityWatchRegistry
{
    private readonly ConcurrentDictionary<EntityWatchKey, ImmutableHashSet<Guid>> _byEntity = new();
    private readonly ConcurrentDictionary<Guid, ImmutableHashSet<EntityWatchKey>> _byConnection = new();

    public void Watch(Guid connectionId, AppEntityType entityType, Guid entityId)
    {
        var key = new EntityWatchKey(entityType, entityId);

        _byEntity.AddOrUpdate(key, _ => [connectionId], (_, set) => set.Add(connectionId));
        _byConnection.AddOrUpdate(connectionId, _ => [key], (_, set) => set.Add(key));
    }

    public void Unwatch(Guid connectionId, AppEntityType entityType, Guid entityId)
    {
        var key = new EntityWatchKey(entityType, entityId);

        Remove(_byEntity, key, connectionId);
        Remove(_byConnection, connectionId, key);
    }

    /// <summary>Returns what the connection was watching — presence has to be republished for each.</summary>
    public IReadOnlyCollection<EntityWatchKey> RemoveConnection(Guid connectionId)
    {
        if (!_byConnection.TryRemove(connectionId, out var keys)) return [];

        foreach (var key in keys)
            Remove(_byEntity, key, connectionId);

        return keys;
    }

    public IReadOnlyCollection<Guid> GetWatchers(AppEntityType entityType, Guid entityId) =>
        _byEntity.GetValueOrDefault(new EntityWatchKey(entityType, entityId), []);

    /// <summary>
    /// Compare-and-swap loop over an immutable set. Removing an emptied bucket is only safe because the
    /// keyed overload of TryRemove checks the value is still the very instance we emptied.
    /// </summary>
    private static void Remove<TKey, TItem>(ConcurrentDictionary<TKey, ImmutableHashSet<TItem>> map,
        TKey key, TItem item) where TKey : notnull
    {
        while (map.TryGetValue(key, out var set))
        {
            var updated = set.Remove(item);
            if (ReferenceEquals(updated, set)) return;

            if (updated.IsEmpty)
            {
                if (map.TryRemove(new KeyValuePair<TKey, ImmutableHashSet<TItem>>(key, set))) return;
            }
            else if (map.TryUpdate(key, updated, set))
            {
                return;
            }
        }
    }
}
