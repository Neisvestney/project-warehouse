namespace ProjectWarehouse.Server.Infrastructure.Realtime;

public class RealtimeNotifier(
    RealtimeConnectionManager connections,
    EntityWatchRegistry watchRegistry,
    ILogger<RealtimeNotifier> logger) : IRealtimeNotifier
{
    public ValueTask PublishAsync(RealtimeAddress address, RealtimeEvent evt, CancellationToken ct = default)
    {
        foreach (var connection in Resolve(address))
        {
            if (connection.TryEnqueue(evt)) continue;

            // Buffer full means the client is not reading. Dropping the connection is safe: events are
            // hints, and the client refetches over REST once it reconnects.
            logger.LogWarning("Realtime connection {ConnectionId} of user {UserId} is not keeping up; closing it",
                connection.Id, connection.UserId);
            connections.Remove(connection.Id);
        }

        return ValueTask.CompletedTask;
    }

    private IEnumerable<RealtimeConnection> Resolve(RealtimeAddress address) => address.Kind switch
    {
        RealtimeAddressKind.User => connections.ByUser(address.UserId),
        RealtimeAddressKind.Watchers => watchRegistry
            .GetWatchers(address.EntityType, address.EntityId)
            .Select(connections.Find)
            .OfType<RealtimeConnection>()
            .Where(c => c.UserId != address.ExceptUserId),
        RealtimeAddressKind.All => connections.All,
        _ => [],
    };
}
