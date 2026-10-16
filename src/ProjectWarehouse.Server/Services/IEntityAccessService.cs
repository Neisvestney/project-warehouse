using System.Security.Claims;
using ProjectWarehouse.Server.Domain;

namespace ProjectWarehouse.Server.Services;

/// <summary>
/// Access to one object, addressed the way the changelog and realtime subscriptions address it —
/// by <see cref="AppEntityType"/> and id. An entity type with no registered rule is never accessible.
/// </summary>
public interface IEntityAccessService
{
    Task<bool> CanViewAsync(AppEntityType entityType, Guid entityId, ClaimsPrincipal user,
        CancellationToken ct = default);

    Task<bool> CanEditAsync(AppEntityType entityType, Guid entityId, ClaimsPrincipal user,
        CancellationToken ct = default);
}
