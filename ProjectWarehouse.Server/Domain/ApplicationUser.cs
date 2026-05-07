using Microsoft.AspNetCore.Identity;

namespace ProjectWarehouse.Server.Domain;

public class ApplicationUser : IdentityUser<Guid>
{
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public int SecurityVersion { get; set; }

    public ICollection<ApplicationUserRole> UserRoles { get; set; } = [];
    public ICollection<UserPermission> UserPermissions { get; set; } = [];
    public ICollection<RefreshToken> RefreshTokens { get; set; } = [];
}
