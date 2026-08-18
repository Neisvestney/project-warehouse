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
}
