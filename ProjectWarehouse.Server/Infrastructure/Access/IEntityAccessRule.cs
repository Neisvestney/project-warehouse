using System.Security.Claims;
using ProjectWarehouse.Server.Domain;

namespace ProjectWarehouse.Server.Infrastructure.Access;

/// <summary>
/// Non-generic face of an access rule, used by the registry and by realtime subscriptions where only
/// <see cref="AppEntityType"/> and an id are known.
/// </summary>
public interface IEntityAccessRule
{
    AppEntityType EntityType { get; }

    Type ClrType { get; }

    Task<bool> CanAsync(ClaimsPrincipal user, AccessLevel level, Guid entityId, CancellationToken ct = default);

    /// <summary>
    /// Verdict from permissions alone, before any object is known. Realtime needs it for collection-level
    /// subscriptions, where there is no id to check against.
    /// </summary>
    Task<AccessVerdict> PrecheckAsync(ClaimsPrincipal user, AccessLevel level, CancellationToken ct = default);
}
