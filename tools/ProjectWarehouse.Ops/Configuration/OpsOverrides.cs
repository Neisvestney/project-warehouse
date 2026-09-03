namespace ProjectWarehouse.Ops.Configuration;

/// The one place that patches instead of replacing. Everything under `registries`, `services` and
/// `targets` swaps whole entries; this section merges field by field, so a machine-local file can
/// supply a key path without restating the target it belongs to.
public sealed class OpsOverrides
{
    public Dictionary<string, TargetOverride> Targets { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);
}

public sealed class TargetOverride
{
    public SshOverride? Ssh { get; set; }
    public string? RepoDir { get; set; }

    public void ApplyTo(TargetConfig target)
    {
        if (RepoDir is { } repoDir)
            target.RepoDir = repoDir;

        if (Ssh is not { } ssh)
            return;

        target.Ssh ??= new SshConfig();
        ssh.ApplyTo(target.Ssh);
    }

    public void MergeFrom(TargetOverride source)
    {
        RepoDir = source.RepoDir ?? RepoDir;

        if (source.Ssh is not { } ssh)
            return;

        if (Ssh is null)
            Ssh = ssh;
        else
            Ssh.MergeFrom(ssh);
    }
}

public sealed class SshOverride
{
    public string? Host { get; set; }
    public int? Port { get; set; }
    public string? User { get; set; }
    public string? KeyPath { get; set; }
    public string? Passphrase { get; set; }

    public void ApplyTo(SshConfig ssh)
    {
        if (Host is { } host)
            ssh.Host = host;

        if (Port is { } port)
            ssh.Port = port;

        if (User is { } user)
            ssh.User = user;

        if (KeyPath is { } keyPath)
            ssh.KeyPath = keyPath;

        if (Passphrase is { } passphrase)
            ssh.Passphrase = passphrase;
    }

    public void MergeFrom(SshOverride source)
    {
        Host = source.Host ?? Host;
        Port = source.Port ?? Port;
        User = source.User ?? User;
        KeyPath = source.KeyPath ?? KeyPath;
        Passphrase = source.Passphrase ?? Passphrase;
    }
}
