namespace ProjectWarehouse.Server.Domain;

public class RefreshToken
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public ApplicationUser User { get; set; } = null!;

    /// <summary>
    /// SHA-256 of the token, hex-encoded. The token itself is 512 bits of CSPRNG output and is never stored,
    /// so a database dump does not hand over live sessions; no salt is needed at that entropy.
    /// </summary>
    public string TokenHash { get; set; } = null!;
    public DateTime ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public bool IsRevoked => RevokedAt.HasValue;
    public bool IsExpired => DateTime.UtcNow >= ExpiresAt;
    public bool IsActive => !IsRevoked && !IsExpired;
}
