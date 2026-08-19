using ProjectWarehouse.Server.Domain;

namespace ProjectWarehouse.Server.Infrastructure.Realtime;

/// <summary>
/// A notice that someone is editing an object. It forbids nothing — saving is never blocked — so it
/// lives in memory and does not survive a restart.
/// </summary>
public record EditLock(
    AppEntityType EntityType,
    Guid EntityId,
    Guid UserId,
    string UserName,
    Guid ConnectionId,
    DateTime AcquiredAt,
    DateTime ExpiresAt);
