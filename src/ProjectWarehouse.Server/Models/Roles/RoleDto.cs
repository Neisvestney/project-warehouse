using ProjectWarehouse.Server.Infrastructure;

namespace ProjectWarehouse.Server.Models.Roles;

public class RoleDto : IHasIdentity
{
    public Guid Id { get; init; }
    public string Name { get; init; } = null!;
}
