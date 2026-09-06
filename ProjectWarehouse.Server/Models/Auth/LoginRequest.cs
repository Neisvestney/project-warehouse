using System.ComponentModel.DataAnnotations;

namespace ProjectWarehouse.Server.Models.Auth;

public class LoginRequest
{
    [Required] [StringLength(256)] public string Username { get; init; } = null!;
    // Bounded because an unbounded value would be fed straight into PBKDF2. Generous enough that no
    // existing passphrase stops working.
    [Required] [StringLength(256)] public string Password { get; init; } = null!;
}
