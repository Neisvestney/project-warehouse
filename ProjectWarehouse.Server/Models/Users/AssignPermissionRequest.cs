using System.ComponentModel.DataAnnotations;

namespace ProjectWarehouse.Server.Models.Users;

public class AssignPermissionRequest
{
    /// <summary>Permission name to assign.</summary>
    [Required] public string Permission { get; init; } = null!;
}
