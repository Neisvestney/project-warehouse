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

    private Task<bool> CanAsync(AppEntityType entityType, Guid entityId, ClaimsPrincipal user,
        AccessLevel level, CancellationToken ct)
    {
        var rule = registry.Find(entityType);
        return rule is null ? Task.FromResult(false) : rule.CanAsync(user, level, entityId, ct);
    }
}
