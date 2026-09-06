using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using ProjectWarehouse.Server.Data;
using ProjectWarehouse.Server.Domain;
using ProjectWarehouse.Server.Infrastructure;
using ProjectWarehouse.Server.Models.Auth;

namespace ProjectWarehouse.Server.Services;

public class TokenService(
    ApplicationDbContext db,
    UserManager<ApplicationUser> userManager,
    IPermissionService permissionService,
    IOptions<JwtOptions> options) : ITokenService
{
    private readonly JwtOptions _jwt = options.Value;

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
        var now = DateTime.UtcNow;
        var rowsAffected = await db.RefreshTokens
            .Where(t => t.TokenHash == hash && t.RevokedAt == null && t.ExpiresAt > now)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, now));

        if (rowsAffected == 0)
            throw new InvalidOperationException("INVALID_REFRESH_TOKEN");

        var userId = await db.RefreshTokens
            .Where(t => t.TokenHash == hash)
            .Select(t => (Guid?)t.UserId)
            .FirstOrDefaultAsync()
            ?? throw new InvalidOperationException("INVALID_REFRESH_TOKEN");

        var user = await userManager.FindByIdAsync(userId.ToString())
            ?? throw new InvalidOperationException("INVALID_REFRESH_TOKEN");

        var claims = await BuildClaimsAsync(user);
        var newAccessToken = CreateJwt(claims);
        var newRefreshToken = await CreateRefreshTokenAsync(user.Id);

        return new TokenResponse
        {
            AccessToken = newAccessToken,
            RefreshToken = newRefreshToken,
            ExpiresIn = _jwt.AccessTokenExpirationMinutes * 60
        };
    }

    public async Task RevokeRefreshTokenAsync(string refreshToken, Guid userId)
    {
        var hash = HashToken(refreshToken);
        var now = DateTime.UtcNow;
        await db.RefreshTokens
            .Where(t => t.TokenHash == hash && t.UserId == userId && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, now));
    }

    private static string HashToken(string token) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    private async Task<List<Claim>> BuildClaimsAsync(ApplicationUser user)
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

        return tokenString;
    }
}
