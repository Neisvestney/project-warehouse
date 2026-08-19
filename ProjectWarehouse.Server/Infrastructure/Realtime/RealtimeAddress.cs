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
    private RealtimeAddress(RealtimeAddressKind kind, Guid userId, AppEntityType entityType, Guid entityId,
        Guid? exceptUserId = null)
    {
        Kind = kind;
        UserId = userId;
        EntityType = entityType;
        EntityId = entityId;
        ExceptUserId = exceptUserId;
    }

    public RealtimeAddressKind Kind { get; }

    public Guid UserId { get; }

    public AppEntityType EntityType { get; }

    public Guid EntityId { get; }

    /// <summary>Connections of this user are skipped — an author is not told about their own change.</summary>
    public Guid? ExceptUserId { get; }

    public static RealtimeAddress ToUser(Guid userId) =>
        new(RealtimeAddressKind.User, userId, AppEntityType.Unknown, Guid.Empty);

    public static RealtimeAddress ToWatchers(AppEntityType entityType, Guid entityId, Guid? exceptUserId = null) =>
        new(RealtimeAddressKind.Watchers, Guid.Empty, entityType, entityId, exceptUserId);

    public static RealtimeAddress ToAll() =>
        new(RealtimeAddressKind.All, Guid.Empty, AppEntityType.Unknown, Guid.Empty);
}
