using ProjectWarehouse.Server.Infrastructure;
using ProjectWarehouse.Server.Models.Roles;
using ProjectWarehouse.Server.Models.Warehouses;

namespace ProjectWarehouse.Server.Models.Users;

public class UserDetailDto : IHasIdentity
{
    public Guid Id { get; init; }
    public string Username { get; init; } = null!;
    public string? Email { get; init; }
    public string? FirstName { get; init; }
    public string? LastName { get; init; }
    public IReadOnlyList<RoleDto> Roles { get; init; } = [];
    public IReadOnlyList<string> DirectPermissions { get; init; } = [];
    public IReadOnlyList<WarehouseSummaryDto> AssignedWarehouses { get; init; } = [];
}
