using System.ComponentModel.DataAnnotations;

namespace ProjectWarehouse.Server.Models.Users;

public class UpdateUserRequest
{
    public string? Email { get; init; }
    public string? FirstName { get; init; }
    public string? LastName { get; init; }
    [Required] public IReadOnlyList<Guid> RoleIds { get; init; } = [];
    [Required] public IReadOnlyList<string> DirectPermissions { get; init; } = [];
    [Required] public IReadOnlyList<Guid> AssignedWarehouseIds { get; init; } = [];
}
