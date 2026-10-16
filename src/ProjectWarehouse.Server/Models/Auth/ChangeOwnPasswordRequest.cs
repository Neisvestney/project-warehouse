using System.ComponentModel.DataAnnotations;

namespace ProjectWarehouse.Server.Models.Auth;

public class ChangeOwnPasswordRequest
{
    [Required] [StringLength(256)] public string CurrentPassword { get; init; } = null!;
    [Required] [StringLength(256)] public string NewPassword { get; init; } = null!;
}
