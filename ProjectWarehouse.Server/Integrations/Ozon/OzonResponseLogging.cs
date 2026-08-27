using ProjectWarehouse.Server.Integrations.Ozon.Generated;

namespace ProjectWarehouse.Server.Integrations.Ozon;

/// <summary>
/// The single writer of Ozon response bodies. Everything that catches an <see cref="OzonApiException" />
/// goes through here, so the rule holds in one place instead of in every catch arm.
/// </summary>
public static class OzonResponseLogging
{
    /// <summary>
    /// Writes the whole response body, untruncated. Reachable only on failure: <see cref="OzonApiException" />
    /// is thrown for a non-2xx status and for a 2xx body that would not deserialize, and for nothing else.
    /// A call that answered 2xx and read cleanly is never logged — that body is a page of cards and would
    /// spend the archive's rotation budget on nothing.
    /// </summary>
    public static void LogFailedResponse(this ILogger logger, OzonApiException ex, LogLevel level) =>
        logger.Log(level, "Ozon call failed ({Reason}, HTTP {OzonStatus}); response body: {OzonResponseBody}",
            ex.StatusCode is >= 200 and < 300 ? "deserialization" : "status",
            ex.StatusCode,
            ex.Response ?? "(empty)");
}
