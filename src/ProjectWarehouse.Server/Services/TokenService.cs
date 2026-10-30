using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using ProjectWarehouse.Server.Data;
using ProjectWarehouse.Server.Domain;
using ProjectWarehouse.Server.Infrastructure;
using ProjectWarehouse.Server.Infrastructure.Observability;
using ProjectWarehouse.Server.Models.Auth;

namespace ProjectWarehouse.Server.Services;

public class TokenService(
    ApplicationDbContext db,
    IPermissionService permissionService,
    SecurityVersionStore versionStore,
    IOptions<JwtOptions> options,
    IDataProtectionProvider dataProtection,
    IEntityLockService locks,
    ILogger<TokenService> logger) : ITokenService
{
    private readonly JwtOptions _jwt = options.Value;
    private readonly IDataProtector _rotationProtector =
        dataProtection.CreateProtector("ProjectWarehouse.RefreshTokenRotation.v1");

    public async Task<TokenResponse> IssueTokensAsync(ApplicationUser user)
    {
        var claims = await BuildClaimsAsync(user);
        var accessToken = CreateJwt(claims);
        var refreshToken = await CreateRefreshTokenAsync(user.Id);

        return new TokenResponse
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresIn = _jwt.AccessTokenExpirationMinutes * 60
        };
    }

    public async Task<TokenResponse> RefreshAsync(string refreshToken)
    {
        var hash = HashToken(refreshToken);
        var userId = await db.RefreshTokens.Where(t => t.TokenHash == hash)
            .Select(t => (Guid?)t.UserId).FirstOrDefaultAsync();
        if (userId is null)
        {
            logger.LogWarning("Refresh rejected: unknown token.");
            throw new InvalidOperationException("INVALID_REFRESH_TOKEN");
        }

        var versionBumped = false;
        var result = await db.Database.ExecuteInTransactionAsync<TokenResponse?>("auth.refresh", async () =>
        {
            await locks.LockAsync<ApplicationUser>(userId.Value, CancellationToken.None);
            var token = await db.RefreshTokens.AsNoTracking().FirstOrDefaultAsync(t => t.TokenHash == hash);
            if (token is null) return null;

            var now = DateTime.UtcNow;
            using var scope = logger.BeginScope(new Dictionary<string, object?>
            {
                ["UserId"] = userId, ["RefreshTokenId"] = token.Id,
                ["RevokedAt"] = token.RevokedAt, ["ExpiresAt"] = token.ExpiresAt,
                ["RevocationAgeSeconds"] = token.RevokedAt is { } revokedAt ? (now - revokedAt).TotalSeconds : null
            });
            var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId);
            if (user is null) return null;

            if (token.RevokedAt is { } revoked)
            {
                if (token.RotationResponseProtected is not null &&
                    now - revoked < TimeSpan.FromSeconds(_jwt.RefreshTokenRecoverySeconds))
                    return await RecoverRotationAsync(token, user, now);

                if (now - revoked >= ReuseGracePeriod)
                {
                    await db.Users.Where(u => u.Id == userId)
                        .ExecuteUpdateAsync(s => s.SetProperty(u => u.SecurityVersion, u => u.SecurityVersion + 1));
                    var revokedCount = await db.RefreshTokens
                        .Where(t => t.UserId == userId && t.RevokedAt == null)
                        .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, now));
                    versionBumped = true;
                    logger.LogWarning("Refresh token reuse detected; all sessions revoked. RevokedSessionCount: {RevokedSessionCount}",
                        revokedCount);
                }
                else logger.LogInformation("Refresh rejected: recently revoked token without a recoverable rotation.");
                return null;
            }

            if (token.ExpiresAt <= now)
            {
                logger.LogInformation("Refresh rejected: expired token.");
                return null;
            }

            var response = await IssueTokensAsync(user);
            var replacementId = db.ChangeTracker.Entries<RefreshToken>()
                .Single(e => e.Entity.TokenHash == HashToken(response.RefreshToken)).Entity.Id;
            var recovery = new RotationRecovery(response, replacementId, user.SecurityVersion,
                new JwtSecurityTokenHandler().ReadJwtToken(response.AccessToken).ValidTo);
            var protectedResponse = _rotationProtector.Protect(JsonSerializer.Serialize(recovery));
            await db.RefreshTokens.Where(t => t.Id == token.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, now)
                    .SetProperty(t => t.RotationResponseProtected, protectedResponse));
            logger.LogInformation("Refresh token rotated. ReplacementTokenId: {ReplacementTokenId}; RecoveryUntil: {RecoveryUntil}",
                replacementId, now.AddSeconds(_jwt.RefreshTokenRecoverySeconds));
            return response;
        });

        if (versionBumped) versionStore.Evict(userId.Value);
        return result ?? throw new InvalidOperationException("INVALID_REFRESH_TOKEN");
    }

    private async Task<TokenResponse?> RecoverRotationAsync(RefreshToken token, ApplicationUser user, DateTime now)
    {
        var recovery = ReadRotationRecovery(token);
        var replacementActive = await db.RefreshTokens.AnyAsync(t => t.Id == recovery.ReplacementTokenId &&
            t.UserId == user.Id && t.RevokedAt == null && t.ExpiresAt > now);
        if (!replacementActive || recovery.SecurityVersion != user.SecurityVersion || recovery.AccessTokenExpiresAt <= now)
        {
            logger.LogInformation("Refresh recovery rejected. ReplacementTokenId: {ReplacementTokenId}; ReplacementActive: {ReplacementActive}; SecurityVersionMatches: {SecurityVersionMatches}",
                recovery.ReplacementTokenId, replacementActive, recovery.SecurityVersion == user.SecurityVersion);
            return null;
        }

        logger.LogInformation("Refresh rotation recovered. ReplacementTokenId: {ReplacementTokenId}", recovery.ReplacementTokenId);
        return new TokenResponse
        {
            AccessToken = recovery.Response.AccessToken, RefreshToken = recovery.Response.RefreshToken,
            ExpiresIn = (int)(recovery.AccessTokenExpiresAt - now).TotalSeconds
        };
    }

    private RotationRecovery ReadRotationRecovery(RefreshToken token)
    {
        try
        {
            return JsonSerializer.Deserialize<RotationRecovery>(
                _rotationProtector.Unprotect(token.RotationResponseProtected!))
                ?? throw new JsonException("Missing rotation response.");
        }
        catch (Exception ex) when (ex is CryptographicException or JsonException)
        {
            logger.LogError(ex, "Stored refresh rotation response cannot be decrypted. RefreshTokenId: {RefreshTokenId}",
                token.Id);
            throw;
        }
    }

    private sealed record RotationRecovery(TokenResponse Response, Guid ReplacementTokenId, int SecurityVersion,
        DateTime AccessTokenExpiresAt);

    public async Task RevokeRefreshTokenAsync(string refreshToken, Guid userId)
    {
        var hash = HashToken(refreshToken);
        await db.Database.ExecuteInTransactionAsync("auth.logout", async () =>
        {
            await locks.LockAsync<ApplicationUser>(userId, CancellationToken.None);
            var token = await db.RefreshTokens.AsNoTracking()
                .FirstOrDefaultAsync(t => t.TokenHash == hash && t.UserId == userId);
            var originalId = token?.Id;
            var chainIds = new HashSet<Guid>();
            // The user lock keeps rotations from extending the chain during logout.
            while (token is not null && chainIds.Add(token.Id))
            {
                if (token.RotationResponseProtected is null) break;
                var recovery = ReadRotationRecovery(token);
                token = await db.RefreshTokens.AsNoTracking()
                    .FirstOrDefaultAsync(t => t.Id == recovery.ReplacementTokenId && t.UserId == userId);
            }

            var now = DateTime.UtcNow;
            var count = await db.RefreshTokens
                .Where(t => chainIds.Contains(t.Id) && t.UserId == userId && t.RevokedAt == null)
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, now));
            logger.LogInformation("Refresh token logout. UserId: {UserId}; RefreshTokenId: {RefreshTokenId}; RotationChainLength: {RotationChainLength}; RevokedSessionCount: {RevokedSessionCount}",
                userId, originalId, chainIds.Count, count);
        });
    }

    // Legacy rotations and explicit revocations have no response to recover.
    private static readonly TimeSpan ReuseGracePeriod = TimeSpan.FromSeconds(10);

    private static string HashToken(string token) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    public async Task<List<Claim>> BuildClaimsAsync(ApplicationUser user)
    {
        // Same set /api/auth/me reports, from the same place — the two must not be able to disagree.
        var permissions = await permissionService.GetEffectivePermissionsAsync(user.Id);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new("name", user.UserName ?? string.Empty),
            new("security_version", user.SecurityVersion.ToString()),
        };

        if (!string.IsNullOrEmpty(user.Email))
            claims.Add(new Claim(JwtRegisteredClaimNames.Email, user.Email));
        if (!string.IsNullOrEmpty(user.FirstName))
            claims.Add(new Claim(JwtRegisteredClaimNames.GivenName, user.FirstName));
        if (!string.IsNullOrEmpty(user.LastName))
            claims.Add(new Claim(JwtRegisteredClaimNames.FamilyName, user.LastName));

        claims.AddRange(permissions.Select(p => new Claim("permission", p)));

        return claims;
    }

    private string CreateJwt(IEnumerable<Claim> claims)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.SecretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _jwt.Issuer,
            audience: _jwt.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_jwt.AccessTokenExpirationMinutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private async Task<string> CreateRefreshTokenAsync(Guid userId)
    {
        var tokenString = Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(64));

        var refreshToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TokenHash = HashToken(tokenString),
            ExpiresAt = DateTime.UtcNow.AddDays(_jwt.RefreshTokenExpirationDays),
        };

        db.RefreshTokens.Add(refreshToken);
        await db.SaveChangesAsync();
        logger.LogInformation("Refresh token issued. UserId: {UserId}; RefreshTokenId: {RefreshTokenId}; ExpiresAt: {ExpiresAt}",
            userId, refreshToken.Id, refreshToken.ExpiresAt);

        return tokenString;
    }
}
