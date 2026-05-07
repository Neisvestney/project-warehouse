using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using ProjectWarehouse.Server.Data;
using ProjectWarehouse.Server.Domain;
using ProjectWarehouse.Server.Models.Auth;

namespace ProjectWarehouse.Server.Services;

public class TokenService(
    ApplicationDbContext db,
    UserManager<ApplicationUser> userManager,
    IConfiguration configuration) : ITokenService
{
    private readonly string _secretKey = configuration["Jwt:SecretKey"]
        ?? throw new InvalidOperationException("Jwt:SecretKey is not configured.");
    private readonly string _issuer = configuration["Jwt:Issuer"] ?? "ProjectWarehouse";
    private readonly string _audience = configuration["Jwt:Audience"] ?? "ProjectWarehouse";
    private readonly int _accessExpirationMinutes = int.TryParse(
        configuration["Jwt:AccessTokenExpirationMinutes"], out var m) ? m : 15;
    private readonly int _refreshExpirationDays = int.TryParse(
        configuration["Jwt:RefreshTokenExpirationDays"], out var d) ? d : 7;

    public async Task<TokenResponse> IssueTokensAsync(ApplicationUser user)
    {
        var claims = await BuildClaimsAsync(user);
        var accessToken = CreateJwt(claims);
        var refreshToken = await CreateRefreshTokenAsync(user.Id);

        return new TokenResponse
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresIn = _accessExpirationMinutes * 60
        };
    }

    public async Task<TokenResponse> RefreshAsync(string refreshToken)
    {
        var now = DateTime.UtcNow;
        var rowsAffected = await db.RefreshTokens
            .Where(t => t.Token == refreshToken && t.RevokedAt == null && t.ExpiresAt > now)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, now));

        if (rowsAffected == 0)
            throw new InvalidOperationException("INVALID_REFRESH_TOKEN");

        var userId = await db.RefreshTokens
            .Where(t => t.Token == refreshToken)
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
            ExpiresIn = _accessExpirationMinutes * 60
        };
    }

    public async Task RevokeRefreshTokenAsync(string refreshToken)
    {
        var token = await db.RefreshTokens.FirstOrDefaultAsync(t => t.Token == refreshToken);
        if (token is { IsActive: true })
        {
            token.RevokedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }
    }

    private async Task<List<Claim>> BuildClaimsAsync(ApplicationUser user)
    {
        var roles = await userManager.GetRolesAsync(user);

        var rolePermissions = await db.RolePermissions
            .Include(rp => rp.Role)
            .Where(rp => rp.Role.UserRoles.Any(ur => ur.UserId == user.Id))
            .Select(rp => rp.Permission)
            .ToListAsync();

        var userPermissions = await db.UserPermissions
            .Where(up => up.UserId == user.Id)
            .Select(up => up.Permission)
            .ToListAsync();

        var allPermissions = rolePermissions.Union(userPermissions).Distinct().ToList();

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

        claims.AddRange(allPermissions.Select(p => new Claim("permission", p)));

        return claims;
    }

    private string CreateJwt(IEnumerable<Claim> claims)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _issuer,
            audience: _audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_accessExpirationMinutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private async Task<string> CreateRefreshTokenAsync(Guid userId)
    {
        var tokenBytes = RandomNumberGenerator.GetBytes(64);
        var tokenString = Convert.ToBase64String(tokenBytes);

        var refreshToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Token = tokenString,
            ExpiresAt = DateTime.UtcNow.AddDays(_refreshExpirationDays),
        };

        db.RefreshTokens.Add(refreshToken);
        await db.SaveChangesAsync();

        return tokenString;
    }
}
