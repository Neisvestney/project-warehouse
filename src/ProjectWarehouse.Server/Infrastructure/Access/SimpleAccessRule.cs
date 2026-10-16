using System.Security.Claims;
using ProjectWarehouse.Server.Data;
using ProjectWarehouse.Server.Domain;

namespace ProjectWarehouse.Server.Infrastructure.Access;

/// <summary>A flat permission pair with no per-object scope — the whole entity type is visible or it is not.</summary>
public class SimpleAccessRule<T>(
    ApplicationDbContext db,
    AppEntityType entityType,
    string view,
    string edit) : EntityAccessRule<T>(entityType)
    where T : class
{
    public override Task<IQueryable<T>> QueryAsync(
        ClaimsPrincipal user, AccessLevel level, CancellationToken ct = default) =>
        Task.FromResult(AccessScope.Has(user, PermissionFor(level)) ? db.Set<T>() : db.Set<T>().Where(_ => false));

    public override Task<AccessVerdict> PrecheckAsync(
        ClaimsPrincipal user, AccessLevel level, CancellationToken ct = default) =>
        CheckWarehouseAsync(user, level, Guid.Empty, ct);

    public override Task<AccessVerdict> CheckAsync(
        ClaimsPrincipal user, AccessLevel level, T entity, CancellationToken ct = default) =>
        CheckWarehouseAsync(user, level, Guid.Empty, ct);

    public override Task<AccessVerdict> CheckWarehouseAsync(
        ClaimsPrincipal user, AccessLevel level, Guid warehouseId, CancellationToken ct = default) =>
        Task.FromResult(AccessScope.Has(user, PermissionFor(level))
            ? AccessVerdict.Allow
            : AccessVerdict.NoPermission);

    private string PermissionFor(AccessLevel level) => level == AccessLevel.View ? view : edit;
}
