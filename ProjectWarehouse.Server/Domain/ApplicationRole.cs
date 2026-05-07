using Microsoft.AspNetCore.Identity;

namespace ProjectWarehouse.Server.Domain;

public class ApplicationRole : IdentityRole<Guid>
{
    public ICollection<ApplicationUserRole> UserRoles { get; set; } = [];
    public ICollection<RolePermission> RolePermissions { get; set; } = [];
}
