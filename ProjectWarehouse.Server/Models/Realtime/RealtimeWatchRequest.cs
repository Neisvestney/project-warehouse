using ProjectWarehouse.Server.Domain;

namespace ProjectWarehouse.Server.Models.Realtime;

public class RealtimeWatchRequest
{
    /// <summary>From the connectionReady event of the stream this subscription belongs to.</summary>
    public required Guid ConnectionId { get; init; }

    public required AppEntityType EntityType { get; init; }

    public required Guid EntityId { get; init; }
}
