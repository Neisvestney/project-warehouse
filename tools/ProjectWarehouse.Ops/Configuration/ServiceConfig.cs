namespace ProjectWarehouse.Ops.Configuration;

public sealed class ServiceConfig
{
    public string Dockerfile { get; set; } = string.Empty;
    public string Context { get; set; } = ".";
    public string Image { get; set; } = string.Empty;
    public string ComposeService { get; set; } = string.Empty;
    public string TagVariable { get; set; } = string.Empty;

    /// Build arg the release tag is passed as. Null leaves the image version-less.
    public string? VersionBuildArg { get; set; }

    public Dictionary<string, string> BuildArgs { get; set; } =
        new(StringComparer.Ordinal);
}
