namespace ProjectWarehouse.Ops.Infrastructure.Docker;

/// Local `docker` invocations. Builds and pushes always happen on this machine — the target never
/// builds anything, it only pulls what the registry already has.
public sealed class DockerCli(string projectDir)
{
    private readonly LocalCommandHost _host = new(projectDir);

    public Task<CommandResult> BuildAsync(
        string dockerfile,
        string context,
        IReadOnlyList<string> tags,
        IReadOnlyDictionary<string, string> buildArgs,
        Action<string> onLine,
        CancellationToken cancellationToken)
    {
        var command = ShellCommand.Of(
            "docker",
            "build",
            "-f",
            Resolve(dockerfile),
            // Plain progress keeps the log linear: the default renderer redraws in place and turns
            // captured output into a wall of escape sequences.
            "--progress=plain");

        foreach (var tag in tags)
            command = command.With("-t", tag);

        foreach (var (key, value) in buildArgs)
            command = command.With("--build-arg", $"{key}={value}");

        command = command.With(Resolve(context));

        return _host.RunWithOutputAsync(command, onLine, cancellationToken);
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
