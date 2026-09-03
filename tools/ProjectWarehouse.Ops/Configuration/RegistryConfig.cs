namespace ProjectWarehouse.Ops.Configuration;

public enum RegistryApi
{
    Harbor,
    DockerV2,
}

public enum RegistryCredentials
{
    Docker,
    Inline,
    Prompt,
}

public sealed class RegistryConfig
{
    public string Url { get; set; } = string.Empty;
    public string Project { get; set; } = string.Empty;
    public RegistryApi Api { get; set; } = RegistryApi.Harbor;
    public RegistryCredentials Credentials { get; set; } = RegistryCredentials.Docker;
    public string? Username { get; set; }
    public string? Password { get; set; }

    /// Host without scheme — what docker actually puts in front of the image path.
    public string Host => Uri.TryCreate(Url, UriKind.Absolute, out var uri) ? uri.Authority : Url;

    public string ImagePrefix => $"{Host}/{Project}";
}
