using ProjectWarehouse.Ops.Configuration;

namespace ProjectWarehouse.Ops.Infrastructure;

/// A connected target: the host to run on plus the paths its config resolves to.
public sealed class TargetContext(
    string name,
    TargetConfig target,
    ICommandHost host,
    string rootDirectory) : IAsyncDisposable
{
    public string Name => name;
    public TargetConfig Config => target;
    public ICommandHost Host => host;

    /// Repo root locally, repoDir over SSH — everything in the target config hangs off it.
    public string RootDirectory => rootDirectory;

    public string ComposeFilePath => Join(rootDirectory, target.ComposeFile);

    public string? EnvFilePath =>
        string.IsNullOrWhiteSpace(target.EnvFile) ? null : Join(rootDirectory, target.EnvFile);

    public static TargetContext Open(string name, TargetConfig target, string repoRoot)
    {
        var root = target.Kind == TargetKind.Ssh
            ? target.RepoDir ?? throw new CommandHostException(
                $"targets.{name}: repoDir is required to locate {target.ComposeFile} on the remote host.")
            : repoRoot;

        return new TargetContext(name, target, CommandHostFactory.Create(target, repoRoot), root);
    }

    public ValueTask DisposeAsync() => host.DisposeAsync();

    private string Join(string root, string relative)
    {
        if (target.Kind == TargetKind.Local)
            return Path.GetFullPath(relative, root);

        return $"{root.TrimEnd('/')}/{relative.Replace('\\', '/').TrimStart('/')}";
    }
}
