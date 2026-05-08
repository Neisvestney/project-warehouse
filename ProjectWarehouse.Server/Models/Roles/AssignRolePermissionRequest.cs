using System.ComponentModel.DataAnnotations;

namespace ProjectWarehouse.Server.Models.Roles;

public class AssignRolePermissionRequest
{
    /// <summary>Permission name to assign.</summary>
    [Required] public string Permission { get; init; } = null!;
}
