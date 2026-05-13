using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using ProjectWarehouse.Server.Domain;
using ProjectWarehouse.Server.Infrastructure;
using ProjectWarehouse.Server.Models;
using ProjectWarehouse.Server.Models.Auth;
using ProjectWarehouse.Server.Services;

namespace ProjectWarehouse.Server.Controllers;

[Route("api/auth")]
public class AuthController(
    UserManager<ApplicationUser> userManager,
    ITokenService tokenService,
    IPermissionService permissionService,
    SecurityVersionStore versionStore) : AppControllerBase
{
    /// <summary>Authenticate with username and password.</summary>
    [HttpPost("login")]
    [ProducesResponseType<TokenResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var user = await userManager.FindByNameAsync(request.Username);
        if (user is null || !await userManager.CheckPasswordAsync(user, request.Password))
            return Unauthorized(ErrorCode.InvalidCredentials, "Username or password is incorrect.");

        var tokens = await tokenService.IssueTokensAsync(user);
        return Ok(tokens);
    }

    /// <summary>Refresh an access token using a valid refresh token.</summary>
    [HttpPost("refresh")]
    [ProducesResponseType<TokenResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Refresh([FromBody] RefreshRequest request)
    {
        try
        {
            var tokens = await tokenService.RefreshAsync(request.RefreshToken);
            return Ok(tokens);
        }
        catch (InvalidOperationException)
        {
            return Unauthorized(ErrorCode.RefreshTokenInvalid, "Refresh token is invalid, expired, or revoked.");
        }
    }

    /// <summary>Revoke the current refresh token (logout).</summary>
    [HttpPost("logout")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout([FromBody] RefreshRequest request)
    {
        await tokenService.RevokeRefreshTokenAsync(request.RefreshToken);
        return NoContent();
    }

    /// <summary>Change the current user's own password (requires current password).</summary>
    [HttpPut("password")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> ChangeOwnPassword([FromBody] ChangeOwnPasswordRequest request)
    {
        var (user, error) = await GetCurrentUserAsync(userManager);
        if (error is not null) return error;
        var me = user!;

        var result = await userManager.ChangePasswordAsync(me, request.CurrentPassword, request.NewPassword);
        if (!result.Succeeded)
        {
            return Problem(PasswordValidationErrorsMapper.MapPasswordValidationErrors(result.Errors));
        }

        await versionStore.BumpAsync(me.Id);
        return NoContent();
    }

    /// <summary>Get the currently authenticated user's info, roles and permissions.</summary>
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType<MeResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Me()
    {
        var (user, error) = await GetCurrentUserAsync(userManager);
        if (error is not null) return error;
        var me = user!;

        var roles = await userManager.GetRolesAsync(me);
        var permissions = await permissionService.GetEffectivePermissionsAsync(me.Id);

        return Ok(new MeResponse
        {
            Id = me.Id,
            Username = me.UserName!,
            Email = me.Email,
            FirstName = me.FirstName,
            LastName = me.LastName,
            Roles = roles.ToList(),
            Permissions = permissions,
        });
    }
}
