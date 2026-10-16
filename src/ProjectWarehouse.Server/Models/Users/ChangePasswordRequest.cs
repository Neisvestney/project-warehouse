using System.ComponentModel.DataAnnotations;

namespace ProjectWarehouse.Server.Models.Users;

public class ChangePasswordRequest
{
    [Required] public string NewPassword { get; init; } = null!;
}
