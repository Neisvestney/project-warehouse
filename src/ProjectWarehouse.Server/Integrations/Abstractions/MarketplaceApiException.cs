namespace ProjectWarehouse.Server.Integrations.Abstractions;

/// <summary>
/// Provider-neutral transport failure. Providers wrap their own client exceptions in this so nothing
/// above the integration layer has to reference a generated type.
/// <para>
/// <see cref="Args"/> carries what the UI needs to phrase the message; the exception message itself is
/// developer-facing English. Nothing here may contain request headers — that is where the API key lives.
/// </para>
/// </summary>
public class MarketplaceApiException(
    string message,
    int? statusCode = null,
    string? responseBody = null,
    Exception? inner = null) : Exception(message, inner)
{
    public int? StatusCode { get; } = statusCode;

    /// <summary>Response body only, already truncated by the provider.</summary>
    public string? ResponseBody { get; } = responseBody;

    public bool IsCredentialsRejected => StatusCode is 401 or 403;

    public IReadOnlyDictionary<string, object> Args
    {
        get
        {
            var args = new Dictionary<string, object>();
            if (StatusCode is { } status)
                args["marketplaceStatus"] = status;
            if (!string.IsNullOrWhiteSpace(ResponseBody))
                args["marketplaceResponse"] = ResponseBody;
            return args;
        }
    }
}
