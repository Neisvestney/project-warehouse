using System.Net.Http.Headers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjectWarehouse.Server.Infrastructure.Observability;

namespace ProjectWarehouse.Server.Controllers;

/// <summary>
/// Proxy through which the frontend hands OTLP over to the collector under the existing JWT
/// authentication, so the collector itself never has to be exposed.
/// </summary>
/// <remarks>
/// The body is never deserialized: OTLP is an opaque payload here, copied straight into the
/// outgoing request.
/// </remarks>
[Route("api/telemetry")]
// telemetry about shipping telemetry is not load on the warehouse — see the metrics section of the spec
[DisableHttpMetrics]
public class TelemetryController(
    IHttpClientFactory httpClientFactory,
    ILogger<TelemetryController> logger) : AppControllerBase
{
    /// <summary>Frontend spans, OTLP/HTTP+JSON.</summary>
    /// <remarks>
    /// The body is forwarded to the collector as is and capped at <c>Observability:MaxClientPayloadBytes</c>;
    /// a larger body is rejected with a bare 413, not an <c>AppProblemDetails</c>.
    /// Always answers 202 with an empty body — a collector that is down is logged as a warning and
    /// never turns into an error for the user.
    /// Requires authentication only.
    /// </remarks>
    [HttpPost("v1/traces")]
    [Authorize]
    [Consumes("application/json")]
    [ClientPayloadSizeLimit]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    public Task<IActionResult> Traces(CancellationToken ct) => ForwardAsync("v1/traces", ct);

    /// <summary>Frontend logs, OTLP/HTTP+JSON.</summary>
    /// <remarks>
    /// Same contract as <c>v1/traces</c>: opaque body, capped at <c>Observability:MaxClientPayloadBytes</c>,
    /// always 202 with an empty body.
    /// Requires authentication only.
    /// </remarks>
    [HttpPost("v1/logs")]
    [Authorize]
    [Consumes("application/json")]
    [ClientPayloadSizeLimit]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    public Task<IActionResult> Logs(CancellationToken ct) => ForwardAsync("v1/logs", ct);

    private async Task<IActionResult> ForwardAsync(string path, CancellationToken ct)
    {
        var client = httpClientFactory.CreateClient(ObservabilityOptions.HttpClientName);
        if (client.BaseAddress is null)
        {
            logger.LogDebug("Client telemetry dropped: no OTLP/HTTP endpoint is configured");
            return Accepted();
        }

        // Read outside the try: past the size limit this throws BadHttpRequestException, and Kestrel
        // turns that into the documented 413. Swallowing it here would answer 202 to a body that was
        // never forwarded. Buffering is safe because the same limit caps it.
        byte[] payload;
        using (var buffer = new MemoryStream())
        {
            await Request.Body.CopyToAsync(buffer, ct);
            payload = buffer.ToArray();
        }

        try
        {
            using var content = new ByteArrayContent(payload);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

            using var response = await client.PostAsync(path, content, ct);
            if (!response.IsSuccessStatusCode)
                logger.LogWarning("Collector answered {StatusCode} for {Path}",
                    (int)response.StatusCode, path);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Failed to forward client telemetry to the collector: {Path}", path);
        }

        return Accepted();
    }
}
