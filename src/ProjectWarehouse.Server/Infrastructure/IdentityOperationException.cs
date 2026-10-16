using Microsoft.AspNetCore.Identity;

namespace ProjectWarehouse.Server.Infrastructure;

/// <summary>
/// Thrown when an Identity manager call fails inside a unit of work. It unwinds the transaction, so the rows
/// Identity already wrote do not survive the failure; the controller turns it into a 422 on <c>root</c>.
/// </summary>
public class IdentityOperationException(IReadOnlyList<IdentityError> errors)
    : Exception(string.Join("; ", errors.Select(e => e.Description))), IExpectedFailure
{
    public IReadOnlyList<IdentityError> Errors { get; } = errors;

    ErrorCode? IExpectedFailure.Code => ErrorCode.ValidationError;

    public IEnumerable<(string Field, ErrorCode Code, string Message, IReadOnlyDictionary<string, object>? Args)>
        ToFieldErrors() =>
        Errors.Select(e => ("root", ErrorCode.ValidationError, e.Description, (IReadOnlyDictionary<string, object>?)null));

    public static void ThrowIfFailed(IdentityResult result)
    {
        if (!result.Succeeded)
            throw new IdentityOperationException([.. result.Errors]);
    }
}
