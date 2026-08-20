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
    /// <remarks>
    /// Anonymous. Returns a <c>TokenResponse</c> — <c>accessToken</c> (JWT), <c>refreshToken</c> (opaque,
    /// single-use) and <c>expiresIn</c> (access token lifetime in seconds).
    /// Returns 401 <c>invalidCredentials</c> when the username is unknown or the password does not match;
    /// the two cases are deliberately indistinguishable.
    /// </remarks>
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
    /// <remarks>
    /// Anonymous. Rotation: the presented refresh token is revoked the instant it is accepted and a whole new
    /// pair is returned — replaying it fails.
    /// Returns 401 <c>refreshTokenInvalid</c> for every failure mode: token unknown, already used or revoked,
    /// past its <c>expiresAt</c>, or its user deleted. The causes are not distinguished, so
    /// <c>refreshTokenExpired</c> and <c>refreshTokenRevoked</c> are never returned here.
    /// Any authenticated endpoint answers 401 when the access token is rejected: <c>tokenInvalid</c> when the
    /// <c>sub</c> claim is missing or unparseable, and a bare 401 from the JWT handler when
    /// <c>security_version</c> no longer matches (<c>tokenOutdated</c>) after a password, role or permission
    /// change. Both mean "call this endpoint", not "log out" — only a failed refresh ends the session.
    /// </remarks>
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
    /// <remarks>
    /// Requires authentication, no permission. Revokes only the refresh token in the body; the access token
    /// stays valid until it expires. Idempotent — an unknown or already-revoked token still answers 204, so
    /// logout has no error codes of its own.
    /// </remarks>
    [HttpPost("logout")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout([FromBody] RefreshRequest request)
    {
        await tokenService.RevokeRefreshTokenAsync(request.RefreshToken);
        return NoContent();
    }

    /// <summary>Change the current user's own password (requires current password).</summary>
    /// <remarks>
    /// Requires authentication, no permission — the caller can only change their own password.
    /// On success bumps the user's <c>security_version</c>, invalidating every access token issued earlier.
    /// Error codes (all password errors use the pseudo-field <c>root</c>):
    /// <list type="bullet">
    ///   <item>401 <c>tokenInvalid</c> — the <c>sub</c> claim is missing or unparseable</item>
    ///   <item>404 <c>userNotFound</c> — the token's user no longer exists</item>
    ///   <item>422 <c>passwordInvalid</c> — <c>currentPassword</c> is wrong</item>
    ///   <item>422 <c>passwordTooShort</c> — args <c>{ minimalLength }</c></item>
    ///   <item>422 <c>passwordAtLeastOneDigit</c> / <c>passwordAtLeastOneUppercase</c> /
    ///         <c>passwordAtLeastOneLowercase</c> — missing character class</item>
    ///   <item>422 <c>validationError</c> — any other Identity failure, message passed through</item>
    /// </list>
    /// </remarks>
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
    /// <remarks>
    /// Requires authentication, no permission. <c>permissions</c> is the effective set — role permissions
    /// unioned with direct ones — read from the database per call, not from the token's claims.
    /// Returns 401 <c>tokenInvalid</c> for a token with no usable <c>sub</c> claim, and 404
    /// <c>userNotFound</c> if the user was deleted while the token was still valid.
    /// </remarks>
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
            FullName = me.FullName,
            Email = me.Email,
            FirstName = me.FirstName,
            LastName = me.LastName,
            Roles = roles.ToList(),
            Permissions = permissions,
        });
    }
}
