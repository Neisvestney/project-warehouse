using ProjectWarehouse.Server.Models.Roles;

namespace ProjectWarehouse.Server.Models.Users;

public class UserDetailDto
{
    public Guid Id { get; init; }
    public string Username { get; init; } = null!;
    public string? Email { get; init; }
    public string? FirstName { get; init; }
    public string? LastName { get; init; }
    public IReadOnlyList<RoleDto> Roles { get; init; } = [];
    public IReadOnlyList<string> DirectPermissions { get; init; } = [];
}
