using System.Linq.Expressions;
using System.Security.Claims;
using ProjectWarehouse.Server.Data;
using ProjectWarehouse.Server.Domain;

namespace ProjectWarehouse.Server.Infrastructure.Access;

/// <summary>
/// The shape almost every warehouse-bound entity uses: a full permission that sees everything, and an
/// <c>_assigned</c> permission that sees only rows whose warehouse is in the user's assignment.
/// </summary>
/// <remarks>
/// Permissions are lists because some entities have more than one permission behaving the same way —
/// an order is viewable by <c>orders.assemble_assigned</c> exactly as it is by <c>orders.view_assigned</c>.
/// </remarks>
public class WarehouseScopedRule<T>(
    ApplicationDbContext db,
    AccessScope scope,
    AppEntityType entityType,
    IReadOnlyList<string> viewAll,
    IReadOnlyList<string> viewAssigned,
    IReadOnlyList<string> editAll,
    IReadOnlyList<string> editAssigned,
    Expression<Func<T, Guid>> warehouse,
    ErrorCode notAssignedCode,
    string notAssignedMessage) : EntityAccessRule<T>(entityType)
    where T : class
{
    private readonly Lazy<Func<T, Guid>> _warehouseOf = new(warehouse.Compile);

    protected ApplicationDbContext Db => db;

    protected AccessScope Scope => scope;

    protected (IReadOnlyList<string> All, IReadOnlyList<string> Assigned) PermissionsFor(AccessLevel level) =>
        level == AccessLevel.View ? (viewAll, viewAssigned) : (editAll, editAssigned);

    public override async Task<IQueryable<T>> QueryAsync(
        ClaimsPrincipal user, AccessLevel level, CancellationToken ct = default)
    {
        var (all, assigned) = PermissionsFor(level);

        if (AccessScope.HasAny(user, all))
            return db.Set<T>();

        if (AccessScope.HasAny(user, assigned))
        {
            var ids = await scope.GetAssignedWarehouseIdsAsync(user, ct);
            if (ids is not null)
                return db.Set<T>().Where(InAssigned(ids));
        }

        return db.Set<T>().Where(_ => false);
    }

    public override async Task<AccessVerdict> PrecheckAsync(
        ClaimsPrincipal user, AccessLevel level, CancellationToken ct = default)
    {
        var (all, assigned) = PermissionsFor(level);

        if (AccessScope.HasAny(user, all))
            return AccessVerdict.Allow;

        if (!AccessScope.HasAny(user, assigned))
            return AccessVerdict.NoPermission;

        return await scope.GetAssignedWarehouseIdsAsync(user, ct) is null
            ? AccessVerdict.TokenInvalid
            : AccessVerdict.Allow;
    }

    public override Task<AccessVerdict> CheckAsync(
        ClaimsPrincipal user, AccessLevel level, T entity, CancellationToken ct = default) =>
        CheckWarehouseAsync(user, level, _warehouseOf.Value(entity), ct);

    public override async Task<AccessVerdict> CheckWarehouseAsync(
        ClaimsPrincipal user, AccessLevel level, Guid warehouseId, CancellationToken ct = default)
    {
        var precheck = await PrecheckAsync(user, level, ct);
        if (!precheck.Allowed) return precheck;

        var (all, _) = PermissionsFor(level);
        if (AccessScope.HasAny(user, all))
            return AccessVerdict.Allow;

        // not null: the precheck above already turned an unusable token into TokenInvalid
        var ids = await scope.GetAssignedWarehouseIdsAsync(user, ct);
        return ids!.Contains(warehouseId)
            ? AccessVerdict.Allow
            : AccessVerdict.NotAssigned(notAssignedCode, notAssignedMessage);
    }

    protected Expression<Func<T, bool>> InAssigned(HashSet<Guid> ids)
    {
        var contains = Expression.Call(
            typeof(Enumerable), nameof(Enumerable.Contains), [typeof(Guid)],
            Expression.Constant(ids), warehouse.Body);

        return Expression.Lambda<Func<T, bool>>(contains, warehouse.Parameters[0]);
    }
}
