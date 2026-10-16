using System.Linq.Expressions;
using ProjectWarehouse.Server.Domain;

namespace ProjectWarehouse.Server.Infrastructure;

public static class UserQueryExtensions
{
    /// <summary>Holds <paramref name="permission"/> through a role or directly — the same union a token carries.</summary>
    public static Expression<Func<ApplicationUser, bool>> HasPermission(string permission) =>
        u => u.UserPermissions.Any(p => p.Permission == permission)
             || u.UserRoles.Any(ur => ur.Role.RolePermissions.Any(rp => rp.Permission == permission));

    public static IQueryable<ApplicationUser> WhereHasPermission(this IQueryable<ApplicationUser> users, string permission) =>
        users.Where(HasPermission(permission));
}
