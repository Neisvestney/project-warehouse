using System.Security.Claims;
using ProjectWarehouse.Server.Domain;
using ProjectWarehouse.Server.Infrastructure.Access;

namespace ProjectWarehouse.Server.Services;

public class EntityAccessService(EntityAccessRegistry registry) : IEntityAccessService
{
    public Task<bool> CanViewAsync(AppEntityType entityType, Guid entityId, ClaimsPrincipal user,
        CancellationToken ct = default) =>
        CanAsync(entityType, entityId, user, AccessLevel.View, ct);

    public Task<bool> CanEditAsync(AppEntityType entityType, Guid entityId, ClaimsPrincipal user,
        CancellationToken ct = default) =>
        CanAsync(entityType, entityId, user, AccessLevel.Edit, ct);

    private async Task<bool> CanAsync(AppEntityType entityType, Guid entityId, ClaimsPrincipal user,
        AccessLevel level, CancellationToken ct)
    {
        var rule = registry.Find(entityType);
        if (rule is null) return false;

        // An empty id addresses the collection rather than a row — that is how the changelog records
        // entities edited as a whole (roles), and there is no object to match a permission against.
        return entityId == Guid.Empty
            ? (await rule.PrecheckAsync(user, level, ct)).Allowed
            : await rule.CanAsync(user, level, entityId, ct);
    }
}
