namespace ProjectWarehouse.Server.Infrastructure;

public class JwtOptions
{
    public const string SectionName = "Jwt";

    /// <summary>HMAC-SHA256 signing key. Has no default — outside development it comes from the environment.</summary>
    public string SecretKey { get; set; } = "";

    public string Issuer { get; set; } = "ProjectWarehouse";

    public string Audience { get; set; } = "ProjectWarehouse";

    /// <summary>
    /// Access token lifetime. Also the window a revoked permission stays usable, since permissions ride in
    /// the token — see the SecurityVersion section of docs/api.md.
    /// </summary>
    public int AccessTokenExpirationMinutes { get; set; } = 15;

    public int RefreshTokenExpirationDays { get; set; } = 7;

    /// <summary>
    /// How long past its expiry a refresh token row is kept. These rows are the only trace a session leaves,
    /// so the window is longer than the token lifetime on purpose.
    /// </summary>
    public int RefreshTokenRetentionDays { get; set; } = 30;

    public string RefreshTokenGcCron { get; set; } = "0 45 3 * * ?";

    /// <summary>Maximum deletions per statement — bounds the transaction size of one GC pass.</summary>
    public int RefreshTokenGcBatchSize { get; set; } = 5000;
}
