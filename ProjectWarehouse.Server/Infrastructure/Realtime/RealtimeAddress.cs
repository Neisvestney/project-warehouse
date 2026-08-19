using ProjectWarehouse.Server.Domain;

namespace ProjectWarehouse.Server.Infrastructure.Realtime;

public enum RealtimeAddressKind
{
    User,
    Watchers,
    All,
}

/// <summary>
/// There is deliberately no "to everyone holding permission X": object events go to explicit
/// subscribers, so connections carry no permission claims.
/// </summary>
public readonly record struct RealtimeAddress
{
    private RealtimeAddress(RealtimeAddressKind kind, Guid userId, AppEntityType entityType, Guid entityId)
    {
        Kind = kind;
        UserId = userId;
        EntityType = entityType;
        EntityId = entityId;
    }

    public RealtimeAddressKind Kind { get; }

    public Guid UserId { get; }

    public AppEntityType EntityType { get; }

    public Guid EntityId { get; }

    public static RealtimeAddress ToUser(Guid userId) =>
        new(RealtimeAddressKind.User, userId, AppEntityType.Unknown, Guid.Empty);

    public static RealtimeAddress ToWatchers(AppEntityType entityType, Guid entityId) =>
        new(RealtimeAddressKind.Watchers, Guid.Empty, entityType, entityId);

    public static RealtimeAddress ToAll() =>
        new(RealtimeAddressKind.All, Guid.Empty, AppEntityType.Unknown, Guid.Empty);
}
