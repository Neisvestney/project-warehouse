using ProjectWarehouse.Server.Domain;
using ProjectWarehouse.Server.Models.Auth;

namespace ProjectWarehouse.Server.Services;

public interface ITokenService
{
    Task<TokenResponse> IssueTokensAsync(ApplicationUser user);
    Task<TokenResponse> RefreshAsync(string refreshToken);
    /// <summary>Revokes the token only if it belongs to <paramref name="userId"/>; a no-op otherwise.</summary>
    Task RevokeRefreshTokenAsync(string refreshToken, Guid userId);
}
