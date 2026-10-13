using System.Security.Claims;
using ProjectWarehouse.Server.Domain;
using ProjectWarehouse.Server.Models.Auth;

namespace ProjectWarehouse.Server.Services;

public interface ITokenService
{
    Task<TokenResponse> IssueTokensAsync(ApplicationUser user);
    Task<TokenResponse> RefreshAsync(string refreshToken);
    /// <summary>Revokes the token only if it belongs to <paramref name="userId"/>; a no-op otherwise.</summary>
    Task RevokeRefreshTokenAsync(string refreshToken, Guid userId);
    /// <summary>The claim set an access token for <paramref name="user"/> carries.</summary>
    Task<List<Claim>> BuildClaimsAsync(ApplicationUser user);
}
