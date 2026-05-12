namespace ProjectWarehouse.Server.Models.Roles;

public class RoleWithPermissionsDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = null!;
    public IReadOnlyList<string> Permissions { get; init; } = [];
}
