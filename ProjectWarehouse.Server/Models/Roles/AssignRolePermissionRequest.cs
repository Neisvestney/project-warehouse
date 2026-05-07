using System.ComponentModel.DataAnnotations;

namespace ProjectWarehouse.Server.Models.Roles;

public class AssignRolePermissionRequest
{
    /// <summary>See GET /api/permissions for valid values.</summary>
    [Required] public string Permission { get; init; } = null!;
}
