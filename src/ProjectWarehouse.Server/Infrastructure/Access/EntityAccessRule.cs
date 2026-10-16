using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using ProjectWarehouse.Server.Domain;

namespace ProjectWarehouse.Server.Infrastructure.Access;

/// <summary>
/// One rule per entity type, expressed as a predicate. The same predicate answers all three questions
/// the app used to answer separately: which rows a list may show, whether a loaded entity is allowed,
/// and whether an id is allowed.
/// </summary>
public abstract class EntityAccessRule<T>(AppEntityType entityType) : IEntityAccessRule
    where T : class
{
    public AppEntityType EntityType => entityType;

    public Type ClrType => typeof(T);

    /// <summary>Rows the user may see or edit. Empty (not all) when the permission is missing.</summary>
    public abstract Task<IQueryable<T>> QueryAsync(
        ClaimsPrincipal user, AccessLevel level, CancellationToken ct = default);

    /// <summary>
    /// Verdict from permissions alone, before any object is known — the prelude every list endpoint runs.
    /// Separate from <see cref="QueryAsync"/> because a list must answer 403/401 rather than return nothing.
    /// </summary>
    public abstract Task<AccessVerdict> PrecheckAsync(
        ClaimsPrincipal user, AccessLevel level, CancellationToken ct = default);

    /// <summary>Verdict for an entity the caller already loaded.</summary>
    public abstract Task<AccessVerdict> CheckAsync(
        ClaimsPrincipal user, AccessLevel level, T entity, CancellationToken ct = default);

    /// <summary>
    /// Verdict before the entity exists — <c>Create</c> validates the warehouse taken from the request body.
    /// Rules without a warehouse scope ignore <paramref name="warehouseId"/>.
    /// </summary>
    public abstract Task<AccessVerdict> CheckWarehouseAsync(
        ClaimsPrincipal user, AccessLevel level, Guid warehouseId, CancellationToken ct = default);

    public async Task<bool> CanAsync(
        ClaimsPrincipal user, AccessLevel level, Guid entityId, CancellationToken ct = default)
    {
        var query = await QueryAsync(user, level, ct);
        return await query.AnyAsync(x => EF.Property<Guid>(x, "Id") == entityId, ct);
    }
}
