using System.Collections.Concurrent;

namespace ProjectWarehouse.Server.Infrastructure.Realtime;

/// <summary>
/// In-memory registry of open streams. Single-node, like SecurityVersionStore —
/// several instances would need a backplane for events published on another node.
/// </summary>
public class RealtimeConnectionManager
{
    private readonly ConcurrentDictionary<Guid, RealtimeConnection> _connections = new();

    public RealtimeConnection Register(Guid userId, string userName)
    {
        var connection = new RealtimeConnection
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            UserName = userName,
        };

        _connections[connection.Id] = connection;
        return connection;
    }

    public void Remove(Guid connectionId)
    {
        if (!_connections.TryRemove(connectionId, out var connection)) return;

        connection.Abort();
        connection.Dispose();
    }

    public RealtimeConnection? Find(Guid connectionId) => _connections.GetValueOrDefault(connectionId);

    /// <summary>
    /// Aborts everything that stopped heartbeating. Only aborts: cancelling the stream is what makes it
    /// run its own teardown, and that already releases the locks, the presence and the registry entry.
    /// </summary>
    public void AbortStale(TimeSpan ttl)
    {
        var cutoff = DateTime.UtcNow - ttl;

        foreach (var connection in _connections.Values)
            if (connection.LastSeenAt < cutoff)
                connection.Abort();
    }

    public IEnumerable<RealtimeConnection> All => _connections.Values;

    public IEnumerable<RealtimeConnection> ByUser(Guid userId) =>
        _connections.Values.Where(c => c.UserId == userId);
}
