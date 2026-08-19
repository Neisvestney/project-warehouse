using ProjectWarehouse.Server.Domain;

namespace ProjectWarehouse.Server.Models.Realtime;

/// <summary>Locks stay one object per call — they are taken by a form, not by a screen.</summary>
public class RealtimeLockRequest
{
    public required Guid ConnectionId { get; init; }

    public required AppEntityType EntityType { get; init; }

    public required Guid EntityId { get; init; }
}
