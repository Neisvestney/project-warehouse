namespace ProjectWarehouse.Ops.Infrastructure.Docker;

/// Local `docker` invocations. Builds and pushes always happen on this machine — the target never
/// builds anything, it only pulls what the registry already has.
public sealed class DockerCli(string projectDir)
{
    private readonly LocalCommandHost _host = new(projectDir);

    /// Writes straight to the terminal, like the push: buildkit draws its own per-step display
    /// only when it sees a console.
    public Task<int> BuildAsync(
        string dockerfile,
        string context,
        IReadOnlyList<string> tags,
        IReadOnlyDictionary<string, string> buildArgs,
        CancellationToken cancellationToken)
    {
        var command = ShellCommand.Of("docker", "build", "-f", Resolve(dockerfile));

        foreach (var tag in tags)
            command = command.With("-t", tag);

        foreach (var (key, value) in buildArgs)
            command = command.With("--build-arg", $"{key}={value}");

        command = command.With(Resolve(context));

        return _host.RunInheritedAsync(command, cancellationToken);
    }

    /// Writes straight to the terminal instead of being captured: docker renders per-layer upload
    /// progress only when it sees a console, and a captured push reports bare state changes.
    public Task<int> PushAsync(string reference, CancellationToken cancellationToken) =>
        _host.RunInheritedAsync(ShellCommand.Of("docker", "push", reference), cancellationToken);

    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken)
    {
        try
        {
            var result = await _host.RunAsync(
                ShellCommand.Of("docker", "version", "--format", "{{.Server.Version}}"),
                cancellationToken);

            return result.Succeeded;
        }
        catch (CommandHostException)
        {
            return false;
        }
    }

    private string Resolve(string path) => Path.GetFullPath(path, projectDir);
}
