using ProjectWarehouse.Server.Domain;

namespace ProjectWarehouse.Server.Infrastructure.Realtime;

/// <summary>
/// A notice that someone is editing an object. It forbids nothing — saving is never blocked — so it
/// lives in memory and does not survive a restart. It has no expiry of its own: the lock lasts exactly
/// as long as the connection that took it, and dies with it.
/// </summary>
public record EditLock(
    AppEntityType EntityType,
    Guid EntityId,
    Guid UserId,
    string UserName,
    Guid ConnectionId,
    DateTime AcquiredAt);
