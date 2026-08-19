using System.Text.Json.Serialization;
using ProjectWarehouse.Server.Domain;

namespace ProjectWarehouse.Server.Infrastructure.Realtime;

/// <summary>
/// Discriminator strings must match the camelCase wire names of <see cref="RealtimeEventType"/> —
/// the client narrows the union by the same value it filters events with.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(ConnectionReadyPayload), "connectionReady")]
[JsonDerivedType(typeof(MarketplaceSyncProgressPayload), "marketplaceSyncProgress")]
[JsonDerivedType(typeof(MarketplaceSyncFinishedPayload), "marketplaceSyncFinished")]
[JsonDerivedType(typeof(EntityChangedPayload), "entityChanged")]
[JsonDerivedType(typeof(EditLockAcquiredPayload), "editLockAcquired")]
[JsonDerivedType(typeof(EditLockReleasedPayload), "editLockReleased")]
[JsonDerivedType(typeof(EntityPresenceChangedPayload), "entityPresenceChanged")]
public abstract class RealtimeEventPayload
{
    [JsonIgnore]
    public abstract RealtimeEventType Type { get; }
}

public class ConnectionReadyPayload : RealtimeEventPayload
{
    [JsonIgnore]
    public override RealtimeEventType Type => RealtimeEventType.ConnectionReady;

    public required Guid ConnectionId { get; init; }
}

/// <summary>
/// Carries no counters on purpose: the event is a hint to refetch, and every counter added to
/// <see cref="MarketplaceSyncRun"/> would otherwise change the event schema too.
/// </summary>
public class MarketplaceSyncProgressPayload : RealtimeEventPayload
{
    [JsonIgnore]
    public override RealtimeEventType Type => RealtimeEventType.MarketplaceSyncProgress;

    public required Guid AccountId { get; init; }

    public required Guid SyncRunId { get; init; }
}

public class MarketplaceSyncFinishedPayload : RealtimeEventPayload
{
    [JsonIgnore]
    public override RealtimeEventType Type => RealtimeEventType.MarketplaceSyncFinished;

    public required Guid AccountId { get; init; }

    public required Guid SyncRunId { get; init; }

    public required MarketplaceSyncStatus Status { get; init; }
}

/// <summary>
/// Published from the changelog service, so it only fires when a comparison actually found changes.
/// The author is excluded from the address — own edits must not be announced as stale.
/// </summary>
public class EntityChangedPayload : RealtimeEventPayload
{
    [JsonIgnore]
    public override RealtimeEventType Type => RealtimeEventType.EntityChanged;

    public required AppEntityType EntityType { get; init; }

    public required Guid EntityId { get; init; }

    public Guid? ByUserId { get; init; }

    public string? ByUserName { get; init; }
}

public class EditLockAcquiredPayload : RealtimeEventPayload
{
    [JsonIgnore]
    public override RealtimeEventType Type => RealtimeEventType.EditLockAcquired;

    public required AppEntityType EntityType { get; init; }

    public required Guid EntityId { get; init; }

    public required Guid UserId { get; init; }

    public required string UserName { get; init; }
}

/// <summary>
/// The holder travels along even though releasing needs no identity: this event is the fallback
/// staleness trigger, and the client must tell its own release apart from someone else's.
/// </summary>
public class EditLockReleasedPayload : RealtimeEventPayload
{
    [JsonIgnore]
    public override RealtimeEventType Type => RealtimeEventType.EditLockReleased;

    public required AppEntityType EntityType { get; init; }

    public required Guid EntityId { get; init; }

    public required Guid UserId { get; init; }

    public required string UserName { get; init; }
}

/// <summary>
/// The whole viewer list, not a join/leave delta: presence has nothing to refetch from, so the event
/// carries the state it announces instead of hinting at it.
/// </summary>
public class EntityPresenceChangedPayload : RealtimeEventPayload
{
    [JsonIgnore]
    public override RealtimeEventType Type => RealtimeEventType.EntityPresenceChanged;

    public required AppEntityType EntityType { get; init; }

    public required Guid EntityId { get; init; }

    /// <summary>Deduplicated by user — several tabs of one person are one viewer.</summary>
    public required IReadOnlyList<RealtimeViewer> Viewers { get; init; }
}
