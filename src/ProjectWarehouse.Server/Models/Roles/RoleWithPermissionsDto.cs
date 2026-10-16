using ProjectWarehouse.Server.Infrastructure;

namespace ProjectWarehouse.Server.Models.Roles;

public class RoleWithPermissionsDto : IHasIdentity
{
    public Guid Id { get; init; }
    public string Name { get; init; } = null!;
    public int Order { get; init; }
    public IReadOnlyList<string> Permissions { get; init; } = [];
}
