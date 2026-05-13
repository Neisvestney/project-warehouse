using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using ProjectWarehouse.Server.Infrastructure;

namespace ProjectWarehouse.Server.Models;

public static class AppProblems
{
    public static AppProblemDetails Root(int status, ErrorCode code, string message, IReadOnlyDictionary<string, object>? args = null) => new()
    {
        Status = status,
        Title = ReasonPhrases.GetReasonPhrase(status),
        Errors = { ["root"] = [MakeError(code, message, args)] }
    };

    public static AppProblemDetails Field(int status, string field, ErrorCode code, string message, IReadOnlyDictionary<string, object>? args = null) => new()
    {
        Status = status,
        Title = ReasonPhrases.GetReasonPhrase(status),
        Errors = { [field] = [MakeError(code, message, args)] }
    };

    public static AppProblemDetails Fields(int status,
        IEnumerable<(string Field, ErrorCode Code, string Message, IReadOnlyDictionary<string, object>? Args)> errors)
    {
        var grouped = errors
            .GroupBy(e => e.Field)
            .ToDictionary(g => g.Key, g => g.Select(e => MakeError(e.Code, e.Message, e.Args)).ToArray());
        return new AppProblemDetails
        {
            Status = status,
            Title = ReasonPhrases.GetReasonPhrase(status),
            Errors = grouped
        };
    }

    public static AppProblemDetails Unauthorized(ErrorCode code, string message) =>
        Root(StatusCodes.Status401Unauthorized, code, message);
    
    public static AppProblemDetails Forbidden(
        ErrorCode code = ErrorCode.PermissionDenied,
        string message = "You do not have permission to perform this action.",
        IReadOnlyDictionary<string, object>? args = null) =>
        Root(StatusCodes.Status403Forbidden, code, message, args);

    public static AppProblemDetails NotFound(ErrorCode code, string message) =>
        Root(StatusCodes.Status404NotFound, code, message);

    public static AppProblemDetails Conflict(ErrorCode code, string message) =>
        Root(StatusCodes.Status409Conflict, code, message);

    public static AppProblemDetails ConflictField(string field, ErrorCode code, string message) =>
        Field(StatusCodes.Status409Conflict, field, code, message);

    public static AppProblemDetails UnprocessableEntity(string field, ErrorCode code, string message) =>
        Field(StatusCodes.Status422UnprocessableEntity, field, code, message);

    public static AppProblemDetails UnprocessableEntities(
        IEnumerable<(string Field, ErrorCode Code, string Message, IReadOnlyDictionary<string, object>? Args)> errors) =>
        Fields(StatusCodes.Status422UnprocessableEntity, errors);

    private static AppFieldError MakeError(ErrorCode code, string message,
        IReadOnlyDictionary<string, object>? args = null) => new()
    {
        Code = code,
        Detail = $"{JsonNamingPolicy.CamelCase.ConvertName(code.ToString())}: {message}",
        Args = args
    };
}
