using System.ComponentModel.DataAnnotations;

namespace ProjectWarehouse.Server.Models.Auth;

public class LoginRequest
{
    [Required] public string Username { get; init; } = null!;
    [Required] public string Password { get; init; } = null!;
}
