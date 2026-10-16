using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace ProjectWarehouse.Server.Infrastructure;

public static class ClaimsPrincipalExtensions
{
    /// <summary>
    /// How the user is named to other users — "редактирует Иванов Иван", not a login. Mirrors
    /// <see cref="Domain.ApplicationUser.FullName" />, reading the claims the token already carries.
    /// </summary>
    public static string GetDisplayName(this ClaimsPrincipal? user)
    {
        if (user is null) return string.Empty;

        var name = string.Join(' ', new[]
            {
                user.FindFirstValue(JwtRegisteredClaimNames.GivenName),
                user.FindFirstValue(JwtRegisteredClaimNames.FamilyName),
            }
            .Where(part => !string.IsNullOrWhiteSpace(part)));

        return string.IsNullOrWhiteSpace(name) ? user.Identity?.Name ?? string.Empty : name;
    }
}
