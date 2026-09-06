using System.ComponentModel.DataAnnotations;

namespace ProjectWarehouse.Server.Models.Auth;

public class RefreshRequest
{
    [Required] [StringLength(256)] public string RefreshToken { get; init; } = null!;
}
