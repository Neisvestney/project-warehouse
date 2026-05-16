using ProjectWarehouse.Server.Infrastructure;

namespace ProjectWarehouse.Server.Models.Roles;

public class RolesListDto : IHasIdentity
{
    public Guid Id { get; set; } = Guid.Empty;
    public IReadOnlyList<RoleWithPermissionsDto> Roles { get; set; } = [];
}
