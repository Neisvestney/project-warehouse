using Neisvestney.Marketplaces.Wildberries;

namespace ProjectWarehouse.Server.Integrations.Wildberries;

/// <summary>
/// The single writer of Wildberries response bodies, on the same terms as <c>OzonResponseLogging</c>: only failed
/// calls are written, and in full.
/// </summary>
public static class WildberriesResponseLogging
{
    public static void LogFailedResponse(this ILogger logger, WildberriesApiException ex, LogLevel level) =>
        logger.Log(level,
            "Wildberries call {WildberriesRequest} failed ({Reason}, HTTP {WildberriesStatus}); "
            + "response body: {WildberriesResponseBody}",
            WildberriesRequestLineHandler.Read(ex) ?? "(unknown)",
            ex.StatusCode is >= 200 and < 300 ? "deserialization" : "status",
            ex.StatusCode,
            ex.Response ?? "(empty)");
}
