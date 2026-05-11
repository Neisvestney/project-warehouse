using System.ComponentModel.DataAnnotations;

namespace ProjectWarehouse.Server.Models.Auth;

public class ChangeOwnPasswordRequest
{
    [Required] public string CurrentPassword { get; init; } = null!;
    [Required] public string NewPassword { get; init; } = null!;
}
