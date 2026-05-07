using ProjectWarehouse.Server.Domain;
using ProjectWarehouse.Server.Models.Auth;

namespace ProjectWarehouse.Server.Services;

public interface ITokenService
{
    Task<TokenResponse> IssueTokensAsync(ApplicationUser user);
    Task<TokenResponse> RefreshAsync(string refreshToken);
    Task RevokeRefreshTokenAsync(string refreshToken);
}
