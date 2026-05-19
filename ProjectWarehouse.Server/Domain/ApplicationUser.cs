using EntityFrameworkCore.Projectables;
using Microsoft.AspNetCore.Identity;

namespace ProjectWarehouse.Server.Domain;

public class ApplicationUser : IdentityUser<Guid>
{
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public int SecurityVersion { get; set; }

    [Projectable]
    public string SearchString =>
        (FirstName ?? "") + " " + (LastName ?? "") + " " + (UserName ?? "") + " " + (Email ?? "");

    public ICollection<ApplicationUserRole> UserRoles { get; set; } = [];
    public ICollection<UserPermission> UserPermissions { get; set; } = [];
    public ICollection<RefreshToken> RefreshTokens { get; set; } = [];
    
    public ICollection<Warehouse> AssignedWarehouses { get; set; } = [];
    public ICollection<InboundOrder> AssignedInboundOrders { get; set; } = [];
}
