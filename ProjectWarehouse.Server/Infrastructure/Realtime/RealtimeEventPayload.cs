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
