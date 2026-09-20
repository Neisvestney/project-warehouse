using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Net.Http.Headers;
using ProjectWarehouse.Server.Data;
using ProjectWarehouse.Server.Domain;
using ProjectWarehouse.Server.Infrastructure;
using ProjectWarehouse.Server.Infrastructure.Access;
using ProjectWarehouse.Server.Models;
using ProjectWarehouse.Server.Services;

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
        string? message = null, 
        IReadOnlyDictionary<string, object>? args = null) =>
        Problem(AppProblems.Forbidden(code,
            message ?? "You do not have permission to perform this action.", args));

    protected ObjectResult NotFound(ErrorCode code, string message) =>
        Problem(AppProblems.NotFound(code, message));

    protected ObjectResult Conflict(ErrorCode code, string message) =>
        Problem(AppProblems.Conflict(code, message));

    protected ObjectResult ConflictField(string field, ErrorCode code, string message) =>
        Problem(AppProblems.ConflictField(field, code, message));

    protected ObjectResult UnprocessableEntity(string field, ErrorCode code, string message,
        IReadOnlyDictionary<string, object>? args = null) =>
        Problem(AppProblems.UnprocessableEntity(field, code, message, args));

    /// <summary>Converts a <see cref="ValidationException"/> into a 422 response using its own field path.</summary>
    protected ObjectResult UnprocessableEntity(ValidationException ex) =>
        UnprocessableEntity(ex.Field, ex.ErrorCode, ex.Message);

    /// <summary>
    /// Converts a <see cref="ValidationException"/> into a 422 response, prepending <paramref name="fieldPrefix"/>
    /// to the exception's field path (e.g. <c>"components"</c> + <c>"inventoryNumber"</c> → <c>"components.inventoryNumber"</c>).
    /// </summary>
    protected ObjectResult UnprocessableEntity(ValidationException ex, string fieldPrefix) =>
        UnprocessableEntity($"{fieldPrefix}.{ex.Field}", ex.ErrorCode, ex.Message);

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
    
    protected Guid? GetCurrentUserId()
    {
        var raw = User.FindFirstValue(JwtRegisteredClaimNames.Sub);
        return Guid.TryParse(raw, out var id) ? id : null;
    }

    /// <summary>Types the browser may render in place. Everything else is served as an attachment.</summary>
    /// <remarks>
    /// image/svg+xml is absent on purpose: an SVG is a scriptable document, and serving one inline
    /// from our own origin is stored XSS.
    /// </remarks>
    private static readonly HashSet<string> InlineContentTypes =
    [
        "image/jpeg", "image/png", "image/webp", "image/gif", "application/pdf",
    ];

    /// <summary>
    /// Serves file content with the caching and disposition rules every binary endpoint shares.
    /// </summary>
    /// <remarks>
    /// Content addressed by id is immutable — replacing a file creates a new row — so the ETag can
    /// be derived from the identifier and lets the browser get a 304 without touching the disk.
    /// </remarks>
    protected FileStreamResult StreamDataFile(DataFileContent content)
    {
        Response.Headers["X-Content-Type-Options"] = "nosniff";

        // passing a download name is what makes ASP.NET Core emit Content-Disposition: attachment
        var downloadName = InlineContentTypes.Contains(content.ContentType) ? null : content.FileName;

        return File(content.Stream, content.ContentType, downloadName,
            lastModified: new DateTimeOffset(content.LastModified, TimeSpan.Zero),
            entityTag: new EntityTagHeaderValue($"\"{content.ETagSource}\""),
            enableRangeProcessing: true);
    }

    /// <summary>Null when access is granted; otherwise the response matching the refusal reason.</summary>
    protected IActionResult? AccessError(AccessVerdict verdict) => verdict.Denial switch
    {
        AccessDenial.None => null,
        AccessDenial.TokenInvalid => Unauthorized(verdict.Code, verdict.Message),
        _ => Forbidden(verdict.Code, verdict.Message),
    };
}