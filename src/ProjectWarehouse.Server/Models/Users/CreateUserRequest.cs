using System.ComponentModel.DataAnnotations;

namespace ProjectWarehouse.Server.Models.Users;

public class CreateUserRequest
{
    [Required] public string Username { get; init; } = null!;
    [Required] public string Password { get; init; } = null!;
    public string? Email { get; init; }
    public string? FirstName { get; init; }
    public string? LastName { get; init; }
}
