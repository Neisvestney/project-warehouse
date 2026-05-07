using System.ComponentModel.DataAnnotations;

namespace ProjectWarehouse.Server.Models.Roles;

public class UpdateRoleRequest
{
    [Required] public string Name { get; init; } = null!;
}
