using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace ProjectWarehouse.Server.Infrastructure;

internal static class ModelStateErrorMapper
{
    public static (ErrorCode Code, IReadOnlyDictionary<string, object>? Args) Resolve(ModelError error)
    {
        if (error.Exception is JsonException jex)
        {
            if (jex.Path is null)
                return (ErrorCode.InvalidJson, null);

            var msg = jex.Message.ToLowerInvariant();
            return msg.Contains("null") || msg.Contains("required")
                ? (ErrorCode.Required, null)
                : (ErrorCode.InvalidFormat, null);
        }

        if (Enum.TryParse<ErrorCode>(error.ErrorMessage, out var parsed))
            return (parsed, null);

        var annotation = error.ErrorMessage.ToLowerInvariant();
        if (annotation.Contains("required"))
            return (ErrorCode.Required, null);

        if (annotation.Contains("maximum length") || annotation.Contains("too long"))
        {
            var max = ExtractNumber(annotation);
            var args = max.HasValue
                ? (IReadOnlyDictionary<string, object>)new Dictionary<string, object> { ["maximalLength"] = max.Value }
                : null;
            return (ErrorCode.TooLong, args);
        }

        if (annotation.Contains("minimum length") || annotation.Contains("too short"))
        {
            var min = ExtractNumber(annotation);
            var args = min.HasValue
                ? (IReadOnlyDictionary<string, object>)new Dictionary<string, object> { ["minimalLength"] = min.Value }
                : null;
            return (ErrorCode.TooShort, args);
        }

        if (annotation.Contains("invalid") || annotation.Contains("not valid"))
            return (ErrorCode.InvalidFormat, null);

        return (ErrorCode.ValidationError, null);
    }

    public static string NormalizeField(string key)
    {
        if (string.IsNullOrEmpty(key)) return "root";
        return char.ToLowerInvariant(key[0]) + key[1..];
    }

    private static readonly Regex NumberInQuotes = new(@"'(\d+)'", RegexOptions.Compiled);

    private static int? ExtractNumber(string text)
    {
        var match = NumberInQuotes.Match(text);
        if (!match.Success) return null;
        return int.TryParse(match.Groups[1].Value, out var n) ? n : null;
    }
}
