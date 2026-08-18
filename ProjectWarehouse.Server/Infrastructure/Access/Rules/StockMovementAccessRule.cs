using System.Security.Claims;
using ProjectWarehouse.Server.Data;
using ProjectWarehouse.Server.Domain;

namespace ProjectWarehouse.Server.Infrastructure.Access.Rules;

/// <summary>
/// Stock movements carry a nullable warehouse — a movement whose warehouse was deleted belongs to no
/// assignment and stays with full-view users, so this cannot reuse <see cref="WarehouseScopedRule{T}"/>.
/// Read-only: movements are a journal, nothing edits them.
/// </summary>
public class StockMovementAccessRule(ApplicationDbContext db, AccessScope scope)
    : EntityAccessRule<StockMovement>(AppEntityType.StockMovement)
{
    public override async Task<IQueryable<StockMovement>> QueryAsync(
        ClaimsPrincipal user, AccessLevel level, CancellationToken ct = default)
    {
        if (level != AccessLevel.View)
            return db.StockMovements.Where(_ => false);

        if (AccessScope.Has(user, Permissions.Statistics.View))
            return db.StockMovements;

        if (AccessScope.Has(user, Permissions.Statistics.ViewAssigned))
        {
            var ids = await scope.GetAssignedWarehouseIdsAsync(user, ct);
            if (ids is not null)
                return db.StockMovements.Where(m => m.WarehouseId != null && ids.Contains(m.WarehouseId.Value));
        }

        return db.StockMovements.Where(_ => false);
    }

    public override Task<AccessVerdict> PrecheckAsync(
        ClaimsPrincipal user, AccessLevel level, CancellationToken ct = default)
    {
        if (level != AccessLevel.View)
            return Task.FromResult(AccessVerdict.NoPermission);

        return AccessScope.Has(user, Permissions.Statistics.View)
               || AccessScope.Has(user, Permissions.Statistics.ViewAssigned)
            ? Task.FromResult(AccessVerdict.Allow)
            : Task.FromResult(AccessVerdict.NoPermission);
    }

    public override Task<AccessVerdict> CheckAsync(
        ClaimsPrincipal user, AccessLevel level, StockMovement entity, CancellationToken ct = default) =>
        CheckWarehouseAsync(user, level, entity.WarehouseId ?? Guid.Empty, ct);

    public override async Task<AccessVerdict> CheckWarehouseAsync(
        ClaimsPrincipal user, AccessLevel level, Guid warehouseId, CancellationToken ct = default)
    {
        if (level != AccessLevel.View)
            return AccessVerdict.NoPermission;

        if (AccessScope.Has(user, Permissions.Statistics.View))
            return AccessVerdict.Allow;

        if (!AccessScope.Has(user, Permissions.Statistics.ViewAssigned))
            return AccessVerdict.NoPermission;

        var ids = await scope.GetAssignedWarehouseIdsAsync(user, ct);
        if (ids is null)
            return AccessVerdict.TokenInvalid;

        return ids.Contains(warehouseId) ? AccessVerdict.Allow : AccessVerdict.NoPermission;
    }
}
