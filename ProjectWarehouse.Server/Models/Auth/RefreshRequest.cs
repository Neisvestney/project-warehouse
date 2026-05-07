using System.ComponentModel.DataAnnotations;

namespace ProjectWarehouse.Server.Models.Auth;

public class RefreshRequest
{
    [Required] public string RefreshToken { get; init; } = null!;
}
