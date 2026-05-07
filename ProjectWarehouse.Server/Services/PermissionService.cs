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

        return rolePermissions.Union(userPermissions).Distinct().ToList();
    }

    public async Task AddRolePermissionAsync(Guid roleId, string permission)
    {
        if (!await db.RolePermissions.AnyAsync(rp => rp.RoleId == roleId && rp.Permission == permission))
        {
            db.RolePermissions.Add(new RolePermission { RoleId = roleId, Permission = permission });
            await db.SaveChangesAsync();
        }
        await BumpForRoleUsersAsync(roleId);
    }

    public async Task RemoveRolePermissionAsync(Guid roleId, string permission)
    {
        var entry = await db.RolePermissions
            .FirstOrDefaultAsync(rp => rp.RoleId == roleId && rp.Permission == permission);
        if (entry is not null)
        {
            db.RolePermissions.Remove(entry);
            await db.SaveChangesAsync();
        }
        await BumpForRoleUsersAsync(roleId);
    }

    public async Task AddUserPermissionAsync(Guid userId, string permission)
    {
        if (!await db.UserPermissions.AnyAsync(up => up.UserId == userId && up.Permission == permission))
        {
            db.UserPermissions.Add(new UserPermission { UserId = userId, Permission = permission });
            await db.SaveChangesAsync();
        }
        await versionStore.BumpAsync(userId);
    }

    public async Task RemoveUserPermissionAsync(Guid userId, string permission)
    {
        var entry = await db.UserPermissions
            .FirstOrDefaultAsync(up => up.UserId == userId && up.Permission == permission);
        if (entry is not null)
        {
            db.UserPermissions.Remove(entry);
            await db.SaveChangesAsync();
        }
        await versionStore.BumpAsync(userId);
    }

    public async Task BumpForRoleUsersAsync(Guid roleId)
    {
        var userIds = await db.Set<ApplicationUserRole>()
            .Where(ur => ur.RoleId == roleId)
            .Select(ur => ur.UserId)
            .ToListAsync();

        if (userIds.Count == 0) return;

        await db.Users
            .Where(u => userIds.Contains(u.Id))
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.SecurityVersion, u => u.SecurityVersion + 1));

        foreach (var id in userIds)
            versionStore.Evict(id);
    }
}
