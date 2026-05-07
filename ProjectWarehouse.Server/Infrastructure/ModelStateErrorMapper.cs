using System.Text.Json;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace ProjectWarehouse.Server.Infrastructure;

internal static class ModelStateErrorMapper
{
    public static ErrorCode Resolve(ModelError error)
    {
        if (error.Exception is JsonException jex)
        {
            if (jex.Path is null)
                return ErrorCode.InvalidJson;

            var msg = jex.Message.ToLowerInvariant();
            return msg.Contains("null") || msg.Contains("required")
                ? ErrorCode.Required
                : ErrorCode.InvalidFormat;
        }

        if (Enum.TryParse<ErrorCode>(error.ErrorMessage, out var parsed))
            return parsed;

        var annotation = error.ErrorMessage.ToLowerInvariant();
        if (annotation.Contains("required"))                                            return ErrorCode.Required;
        if (annotation.Contains("maximum length") || annotation.Contains("too long"))  return ErrorCode.TooLong;
        if (annotation.Contains("minimum length") || annotation.Contains("too short")) return ErrorCode.TooShort;
        if (annotation.Contains("invalid") || annotation.Contains("not valid"))        return ErrorCode.InvalidFormat;
        return ErrorCode.ValidationError;
    }

    public static string NormalizeField(string key)
    {
        if (string.IsNullOrEmpty(key)) return "root";
        return char.ToLowerInvariant(key[0]) + key[1..];
    }
}
