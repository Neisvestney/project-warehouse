using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using ProjectWarehouse.Server.Models;

namespace ProjectWarehouse.Server.Infrastructure;

public class PasswordValidationErrorsMapper
{
    public static AppProblemDetails MapPasswordValidationErrors(IEnumerable<IdentityError> errors)
    {
        var proccessedErrrors = errors.Select(e => MapPasswordValidationError(e));
        return AppProblems.UnprocessableEntities(proccessedErrrors);
    }

    private static (string, ErrorCode, string, IReadOnlyDictionary<string, object>?) MapPasswordValidationError(IdentityError error)
    {
        if (error.Description.Contains("must be at least"))
        {
            var min = ExtractNumber(error.Description);
            var args = min.HasValue
                ? (IReadOnlyDictionary<string, object>)new Dictionary<string, object> { ["minimalLength"] = min.Value }
                : null;
            return ("root", ErrorCode.PasswordTooShort, error.Description, args);
        }
        
        if (error.Description.Contains("must have at least one digit"))
        {
            return ("root", ErrorCode.PasswordAtLeastOneDigit, error.Description, null);
        }
        
        if (error.Description.Contains("must have at least one uppercase"))
        {
            return ("root", ErrorCode.PasswordAtLeastOneUppercase, error.Description, null);
        }
        
        if (error.Description.Contains("must have at least one lowercase"))
        {
            return ("root", ErrorCode.PasswordAtLeastOneLowercase, error.Description, null);
        }
        
        if (error.Description.Contains("Incorrect password"))
        {
            return ("root", ErrorCode.PasswordInvalid, error.Description, null);
        }
        
        return ("root", ErrorCode.ValidationError, error.Description, null);
    }
    
    private static readonly Regex NumberAfterAtLeast = new(@"at least (\d+)", RegexOptions.Compiled);

    private static int? ExtractNumber(string text)
    {
        var match = NumberAfterAtLeast.Match(text);
        if (!match.Success) return null;
        return int.TryParse(match.Groups[1].Value, out var n) ? n : null;
    }
}