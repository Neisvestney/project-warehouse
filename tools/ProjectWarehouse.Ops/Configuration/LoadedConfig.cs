namespace ProjectWarehouse.Ops.Configuration;

/// The merged config plus the files it came from, innermost first.
public sealed record LoadedConfig(
    OpsConfig Config,
    IReadOnlyList<string> SourceChain,
    string ProjectDir);
