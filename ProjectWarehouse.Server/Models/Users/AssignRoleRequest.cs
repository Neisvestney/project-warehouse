using System.ComponentModel.DataAnnotations;

namespace ProjectWarehouse.Server.Models.Users;

public class AssignRoleRequest
{
    [Required] public Guid RoleId { get; init; }
}