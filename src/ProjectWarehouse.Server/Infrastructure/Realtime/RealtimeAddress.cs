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
        Guid? exceptUserId = null, Guid? exceptConnectionId = null, IReadOnlySet<Guid>? onlyUserIds = null)
    {
        Kind = kind;
        UserId = userId;
        EntityType = entityType;
        EntityId = entityId;
        ExceptUserId = exceptUserId;
        ExceptConnectionId = exceptConnectionId;
        OnlyUserIds = onlyUserIds;
    }

    public RealtimeAddressKind Kind { get; }

    public Guid UserId { get; }

    public AppEntityType EntityType { get; }

    public Guid EntityId { get; }

    /// <summary>Connections of this user are skipped — an author is not told about their own change.</summary>
    public Guid? ExceptUserId { get; }

    /// <summary>
    /// Only this one connection is skipped — the tab that made the change. Other tabs or devices of the
    /// same user are still watchers and get told.
    /// </summary>
    public Guid? ExceptConnectionId { get; }

    /// <summary>
    /// When set, only watchers belonging to these users are told — for a collection whose rows differ per
    /// user, where every other watcher would receive ids of objects it never sees.
    /// </summary>
    public IReadOnlySet<Guid>? OnlyUserIds { get; }

    public static RealtimeAddress ToUser(Guid userId) =>
        new(RealtimeAddressKind.User, userId, AppEntityType.Unknown, Guid.Empty);

    public static RealtimeAddress ToWatchers(AppEntityType entityType, Guid entityId, Guid? exceptUserId = null,
        Guid? exceptConnectionId = null, IReadOnlySet<Guid>? onlyUserIds = null) =>
        new(RealtimeAddressKind.Watchers, Guid.Empty, entityType, entityId, exceptUserId, exceptConnectionId,
            onlyUserIds);

    public static RealtimeAddress ToAll() =>
        new(RealtimeAddressKind.All, Guid.Empty, AppEntityType.Unknown, Guid.Empty);
}
