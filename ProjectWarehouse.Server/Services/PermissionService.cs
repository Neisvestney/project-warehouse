using Microsoft.EntityFrameworkCore;
using ProjectWarehouse.Server.Data;
using ProjectWarehouse.Server.Domain;
using ProjectWarehouse.Server.Infrastructure;

namespace ProjectWarehouse.Server.Services;

public class PermissionService(
    ApplicationDbContext db,
    SecurityVersionStore versionStore) : IPermissionService
{
    public async Task<IReadOnlyList<string>> GetEffectivePermissionsAsync(Guid userId)
    {
        var rolePermissions = await db.RolePermissions
            .Where(rp => rp.Role.UserRoles.Any(ur => ur.UserId == userId))
            .Select(rp => rp.Permission)
            .ToListAsync();

        var userPermissions = await db.UserPermissions
            .Where(up => up.UserId == userId)
            .Select(up => up.Permission)
            .ToListAsync();

        return rolePermissions.Union(userPermissions).ToList();
    }

    public async Task BumpForRoleUsersAsync(Guid roleId)
    {
        var userIds = await db.Set<ApplicationUserRole>()
            .Where(ur => ur.RoleId == roleId)
            .Select(ur => ur.UserId)
            .ToListAsync();

        await BumpUsersAsync(userIds);
    }

    public async Task BumpUsersAsync(IEnumerable<Guid> userIds)
    {
        var ids = userIds.ToList();
        if (ids.Count == 0) return;

        await db.Users
            .Where(u => ids.Contains(u.Id))
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.SecurityVersion, u => u.SecurityVersion + 1));

        foreach (var id in ids)
            versionStore.Evict(id);
    }
}
