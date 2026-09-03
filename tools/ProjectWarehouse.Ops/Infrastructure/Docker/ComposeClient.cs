using System.Text.Json;
using System.Text.Json.Serialization;
using ProjectWarehouse.Ops.Infrastructure;

namespace ProjectWarehouse.Ops.Infrastructure.Docker;

public sealed record ComposeService(
    [property: JsonPropertyName("Service")] string Name,
    [property: JsonPropertyName("State")] string State,
    [property: JsonPropertyName("Status")] string Status,
    [property: JsonPropertyName("Health")] string? Health,
    [property: JsonPropertyName("Image")] string Image);

/// Compose commands for one target. The compose file is always addressed absolutely, which also
/// fixes the project name — compose derives it from the file's directory.
public sealed class ComposeClient(TargetContext target)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public ShellCommand Command(params string[] args)
    {
        var command = ShellCommand.Of("docker", "compose", "-f", target.ComposeFilePath);

        // Named explicitly: compose otherwise loads whatever .env sits next to the compose file,
        // which is only the file the tool rewrites by coincidence.
        if (target.EnvFilePath is { } envFile)
            command = command.With("--env-file", envFile);

        return command.With(args);
    }

    public Task<CommandResult> PullAsync(
        IEnumerable<string> services, CancellationToken cancellationToken) =>
        target.Host.RunAsync(Command("pull").With([.. services]), cancellationToken);

    public Task<CommandResult> StopAsync(
        IEnumerable<string> services, CancellationToken cancellationToken) =>
        target.Host.RunAsync(Command("stop").With([.. services]), cancellationToken);

    public Task<CommandResult> CopyAsync(
        string source, string destination, CancellationToken cancellationToken) =>
        target.Host.RunAsync(Command("cp", source, destination), cancellationToken);

    public Task<CommandResult> UpAsync(CancellationToken cancellationToken) =>
        target.Host.RunAsync(Command("up", "-d", "--remove-orphans"), cancellationToken);

    public Task<CommandResult> LogsAsync(
        IEnumerable<string> services, int tail, CancellationToken cancellationToken) =>
        target.Host.RunAsync(
            Command("logs", "--no-color", $"--tail={tail}").With([.. services]), cancellationToken);

    /// <param name="includeStopped">
    /// Health checks must not see leftovers: an exited container from a previous run would keep a
    /// perfectly healthy service looking unsettled until the timeout.
    /// </param>
    public async Task<IReadOnlyList<ComposeService>> PsAsync(
        CancellationToken cancellationToken, bool includeStopped = true)
    {
        var command = includeStopped
            ? Command("ps", "--all", "--format", "json")
            : Command("ps", "--format", "json");

        var result = await target.Host.RunAsync(command, cancellationToken);

        if (!result.Succeeded)
            throw new CommandHostException($"docker compose ps failed: {result.FailureMessage}");

        return Parse(result.StdOut);
    }

    /// One JSON object per line on modern compose, a JSON array on older builds.
    private static List<ComposeService> Parse(string output)
    {
        var trimmed = output.TrimStart();
        if (trimmed.StartsWith('['))
        {
            try
            {
                return JsonSerializer.Deserialize<List<ComposeService>>(trimmed, JsonOptions) ?? [];
            }
            catch (JsonException)
            {
                return [];
            }
        }

        var services = new List<ComposeService>();
        foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = line.Trim();
            if (candidate.Length == 0)
                continue;

            // compose mixes plain notices into stdout across versions — a line that is not an
            // object is not a container, and reading status must not die on one.
            try
            {
                if (JsonSerializer.Deserialize<ComposeService>(candidate, JsonOptions) is { } parsed)
                    services.Add(parsed);
            }
            catch (JsonException)
            {
                // ignored
            }
        }

        return services;
    }
}
