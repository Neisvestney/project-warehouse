namespace ProjectWarehouse.Server.Infrastructure.Observability;

public class ObservabilityOptions
{
    public const string SectionName = "Observability";

    /// <summary>Name of the <see cref="IHttpClientFactory"/> client that reaches the collector.</summary>
    public const string HttpClientName = "otlp-collector";

    /// <summary>
    /// OTLP receiver of the collector. In a container this is the service name in the docker network.
    /// An empty value switches the export off entirely.
    /// </summary>
    public string OtlpEndpoint { get; set; } = "http://otel-collector:4317";

    /// <summary>
    /// OTLP/HTTP receiver of the same collector, used by the telemetry proxy endpoint: the frontend
    /// speaks OTLP/HTTP+JSON, which the gRPC port of <see cref="OtlpEndpoint"/> does not accept.
    /// An empty value switches the forwarding off.
    /// </summary>
    public string OtlpHttpEndpoint { get; set; } = "http://otel-collector:4318";

    public string ServiceName { get; set; } = "projectwarehouse.server";

    /// <summary>Share of traces that reaches the archive.</summary>
    public double TraceSampleRatio { get; set; } = 1.0;

    /// <summary>Ceiling for the body of a single OTLP request coming from the frontend, bytes.</summary>
    public int MaxClientPayloadBytes { get; set; } = 1024 * 1024;

    /// <summary>How long the proxy endpoint waits for the collector before giving the batch up.</summary>
    public int CollectorTimeoutSeconds { get; set; } = 10;
}
