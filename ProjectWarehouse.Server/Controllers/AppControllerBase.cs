using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using ProjectWarehouse.Server.Domain;
using ProjectWarehouse.Server.Infrastructure;
using ProjectWarehouse.Server.Models;

namespace ProjectWarehouse.Server.Controllers;

[ApiController]
[ProducesErrorResponseType(typeof(AppProblemDetails))]
[ProducesResponseType<AppProblemDetails>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<AppProblemDetails>(StatusCodes.Status403Forbidden)]
public abstract class AppControllerBase : ControllerBase
{
    protected ObjectResult Problem(AppProblemDetails details) =>
        new(details) { StatusCode = details.Status };

    protected ObjectResult Unauthorized(ErrorCode code, string message) =>
        Problem(AppProblems.Unauthorized(code, message));

    protected ObjectResult Forbidden(
        ErrorCode code = ErrorCode.PermissionDenied,
        string? message = null) =>
        Problem(AppProblems.Forbidden(code,
            message ?? "You do not have permission to perform this action."));

    protected ObjectResult NotFound(ErrorCode code, string message) =>
        Problem(AppProblems.NotFound(code, message));

    protected ObjectResult Conflict(ErrorCode code, string message) =>
        Problem(AppProblems.Conflict(code, message));

    protected ObjectResult ConflictField(string field, ErrorCode code, string message) =>
        Problem(AppProblems.ConflictField(field, code, message));

    protected ObjectResult UnprocessableEntity(string field, ErrorCode code, string message) =>
        Problem(AppProblems.UnprocessableEntity(field, code, message));

    protected async Task<(ApplicationUser? User, IActionResult? Error)> GetCurrentUserAsync(
        UserManager<ApplicationUser> userManager)
    {
        var rawId = User.FindFirstValue(JwtRegisteredClaimNames.Sub);
        if (!Guid.TryParse(rawId, out var userId))
            return (null, Unauthorized(ErrorCode.TokenInvalid, "Token does not contain a valid user ID."));

        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null)
            return (null, NotFound(ErrorCode.UserNotFound, "User not found."));

        return (user, null);
    }
}
