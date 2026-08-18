using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using ProjectWarehouse.Server.Data;

namespace ProjectWarehouse.Server.Infrastructure.Access;

/// <summary>
/// Per-request cache of the things every access rule asks for. The assigned warehouse set used to be
/// re-queried by each check — an order request read it two or three times.
/// </summary>
public class AccessScope(ApplicationDbContext db)
{
    private Guid? _cachedFor;
    private HashSet<Guid>? _cached;

    public static Guid? GetUserId(ClaimsPrincipal user) =>
        Guid.TryParse(user.FindFirstValue(JwtRegisteredClaimNames.Sub), out var id) ? id : null;

    public static bool Has(ClaimsPrincipal user, string permission) =>
        user.HasClaim("permission", permission);

    public static bool HasAny(ClaimsPrincipal user, IReadOnlyList<string> permissions)
    {
        for (var i = 0; i < permissions.Count; i++)
            if (Has(user, permissions[i]))
                return true;

        return false;
    }

    /// <summary>
    /// How far to narrow a query for this user. For queries that cannot be expressed as a plain rule
    /// predicate — inventory counts inside subqueries, transfers spanning two warehouses.
    /// </summary>
    public async Task<WarehouseNarrowing> GetWarehouseNarrowingAsync(
        ClaimsPrincipal user, string fullPermission, CancellationToken ct = default)
    {
        if (Has(user, fullPermission))
            return WarehouseNarrowing.Unrestricted;

        var ids = await GetAssignedWarehouseIdsAsync(user, ct);
        return ids is null ? WarehouseNarrowing.TokenInvalid : WarehouseNarrowing.RestrictedTo(ids);
    }

    /// <summary>Null when the token carries no usable user id — callers treat that as 401, not as "nothing assigned".</summary>
    public async Task<HashSet<Guid>?> GetAssignedWarehouseIdsAsync(ClaimsPrincipal user, CancellationToken ct = default)
    {
        var userId = GetUserId(user);
        if (userId is null) return null;

        if (_cached is not null && _cachedFor == userId) return _cached;

        var ids = await db.Users
            .Where(u => u.Id == userId.Value)
            .SelectMany(u => u.AssignedWarehouses)
            .Select(w => w.Id)
            .ToListAsync(ct);

        _cachedFor = userId;
        _cached = [..ids];
        return _cached;
    }
}
