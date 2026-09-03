namespace ProjectWarehouse.Ops.Configuration;

public sealed class OpsConfig
{
    public string? IncludeConfig { get; set; }

    public LocalPathsConfig? Local { get; set; }

    public Dictionary<string, RegistryConfig> Registries { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, ServiceConfig> Services { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, TargetConfig> Targets { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);
}

public sealed class LocalPathsConfig
{
    public string BackupsDir { get; set; } = "./backups";
    public string TelemetryArchiveDir { get; set; } = "./telemetry-archive";
}
