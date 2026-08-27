namespace ProjectWarehouse.Server.Infrastructure.Observability;

public class ObservabilityOptions
{
    public const string SectionName = "Observability";

    /// <summary>
    /// OTLP receiver of the collector. In a container this is the service name in the docker network.
    /// An empty value switches the export off entirely.
    /// </summary>
    public string OtlpEndpoint { get; set; } = "http://otel-collector:4317";

    public string ServiceName { get; set; } = "projectwarehouse.server";

    /// <summary>Share of traces that reaches the archive.</summary>
    public double TraceSampleRatio { get; set; } = 1.0;

    /// <summary>Ceiling for the body of a single OTLP request coming from the frontend, bytes.</summary>
    public int MaxClientPayloadBytes { get; set; } = 1024 * 1024;
}
