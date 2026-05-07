using System.ComponentModel.DataAnnotations;

namespace ProjectWarehouse.Server.Models.Users;

public class AssignPermissionRequest
{
    /// <summary>See GET /api/permissions for valid values.</summary>
    [Required] public string Permission { get; init; } = null!;
}