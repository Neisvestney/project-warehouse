namespace ProjectWarehouse.Ops.Configuration;

public enum TargetKind
{
    Local,
    Ssh,
}

public sealed class TargetConfig
{
    public TargetKind Kind { get; set; } = TargetKind.Ssh;
    public bool Danger { get; set; }
    public SshConfig? Ssh { get; set; }
    public string? RepoDir { get; set; }
    public bool GitPull { get; set; }
    public string ComposeFile { get; set; } = string.Empty;
    public string? EnvFile { get; set; }
    public string? RegistryVariable { get; set; }
    public string? PullsFrom { get; set; }
    public List<string> Services { get; set; } = [];

    /// Logical name -> compose volume name.
    public Dictionary<string, string> Volumes { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);

    public PostgresConfig? Postgres { get; set; }
}

public sealed class SshConfig
{
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 22;
    public string User { get; set; } = string.Empty;
    public string? KeyPath { get; set; }
    public string? Passphrase { get; set; }
}

public sealed class PostgresConfig
{
    public string Service { get; set; } = "postgres";
    public string User { get; set; } = "postgres";
    public string Database { get; set; } = string.Empty;
}
