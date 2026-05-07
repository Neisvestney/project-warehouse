using System.ComponentModel.DataAnnotations;

namespace ProjectWarehouse.Server.Models.Roles;

public class CreateRoleRequest
{
    [Required] public string Name { get; init; } = null!;
}
