using ProjectWarehouse.Server.Domain;

namespace ProjectWarehouse.Server.Infrastructure.Realtime;

/// <summary>
/// A notice that someone is editing an object. It forbids nothing — saving is never blocked — so it
/// lives in memory and does not survive a restart. It has no expiry of its own: the lock lasts exactly
/// as long as the connections holding it, and dies with the last of them.
/// </summary>
/// <remarks>
/// Carries no connection id on purpose: one person may hold the same object from several tabs, and the
/// notice their colleagues see is the same either way.
/// </remarks>
public record EditLock(
    AppEntityType EntityType,
    Guid EntityId,
    Guid UserId,
    string UserName,
    DateTime AcquiredAt);
