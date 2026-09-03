using ProjectWarehouse.Ops.Configuration;
using ProjectWarehouse.Ops.Infrastructure;
using ProjectWarehouse.Ops.Infrastructure.Docker;
using ProjectWarehouse.Ops.Infrastructure.Git;

namespace ProjectWarehouse.Ops.Services;

public sealed record DeploySelection(string ServiceName, ServiceConfig Service, string Version);

/// What the target looks like before anything is touched. A null value in <see cref="Snapshot"/>
/// means the variable was absent, which rollback has to be able to restore just as literally.
public sealed record DeployPreflight(
    EnvFile Env,
    GitState? Git,
    IReadOnlyDictionary<string, string?> Snapshot,
    string? ExpectedRegistryValue);

public sealed record DeployOutcome(
    bool Succeeded, string? Failure, string? Logs, bool RolledBack, string? RollbackFailure);

public sealed class DeploymentException(string message) : Exception(message);

public sealed class DeploymentService(TargetContext target)
{
    private readonly ComposeClient _compose = new(target);
    private readonly GitClient _git = new(target);

    public async Task<DeployPreflight> PreflightAsync(
        RegistryConfig? registry,
        IReadOnlyList<string> variables,
        CancellationToken cancellationToken)
    {
        if (target.EnvFilePath is not { } path)
            throw new DeploymentException($"targets.{target.Name}: envFile is required to deploy.");

        var content = await target.Host.ReadFileAsync(path, cancellationToken)
            ?? throw new DeploymentException($"{path} not found on the target.");

        var env = EnvFile.Parse(content);

        if (env.Duplicates(variables) is { Count: > 0 } duplicates)
        {
            throw new DeploymentException(
                $"{path} defines {string.Join(", ", duplicates)} more than once. Compose reads the "
                    + "last definition and an editor sees the first, so leave exactly one of each.");
        }

        var present = env.GetAll(variables);
        var snapshot = variables.ToDictionary(
            key => key,
            key => present.TryGetValue(key, out var value) ? value : null,
            StringComparer.Ordinal);

        var git = target.Config.GitPull ? await _git.ReadStateAsync(cancellationToken) : null;

        return new DeployPreflight(env, git, snapshot, registry?.ImagePrefix);
    }

    public async Task<DeployOutcome> ExecuteAsync(
        DeployPreflight preflight,
        IReadOnlyDictionary<string, string> values,
        IReadOnlyList<string> composeServices,
        TimeSpan healthTimeout,
        Action<string> onStep,
        CancellationToken cancellationToken)
    {
        if (healthTimeout <= TimeSpan.Zero)
            throw new DeploymentException("healthTimeout must be positive.");

        if (target.Config.GitPull)
        {
            if (preflight.Git is { } git && !git.SafeToDeploy)
            {
                throw new DeploymentException(
                    git.StatusError is { } error
                        ? $"Cannot read the working tree at {target.RootDirectory}: {error}"
                        : $"{target.RootDirectory} has uncommitted changes. Clean it before deploying.");
            }

            onStep("git pull");
            var pull = await _git.PullAsync(cancellationToken);
            if (!pull.Succeeded)
                return Failed($"git pull failed: {pull.FailureMessage}");
        }

        onStep("writing env file");
        await WriteEnvAsync(preflight.Env, ToNullable(values), backup: true, cancellationToken);

        // Everything past this point has changed the target, so every exit runs through the
        // rollback — including cancellation and a connection that dies mid-step.
        try
        {
            onStep("docker compose pull");
            var pullImages = await _compose.PullAsync(composeServices, cancellationToken);
            if (!pullImages.Succeeded)
                return await RollbackAsync(
                    preflight, $"compose pull failed: {pullImages.FailureMessage}", composeServices, onStep);

            onStep("docker compose up -d");
            var up = await _compose.UpAsync(cancellationToken);
            if (!up.Succeeded)
                return await RollbackAsync(
                    preflight, $"compose up failed: {up.FailureMessage}", composeServices, onStep);

            onStep("waiting for health");
            var unhealthy = await WaitForHealthAsync(composeServices, healthTimeout, cancellationToken);
            if (unhealthy is not null)
                return await RollbackAsync(preflight, unhealthy, composeServices, onStep);
        }
        catch (Exception ex)
        {
            var reason = ex is OperationCanceledException ? "cancelled" : ex.Message;
            return await RollbackAsync(preflight, reason, composeServices, onStep);
        }

        return new DeployOutcome(true, null, null, false, null);
    }

    private static DeployOutcome Failed(string message) =>
        new(false, message, null, false, null);

    private static Dictionary<string, string?> ToNullable(IReadOnlyDictionary<string, string> values) =>
        values.ToDictionary(pair => pair.Key, pair => (string?)pair.Value, StringComparer.Ordinal);

    /// Runs without a cancellation token on purpose: it exists to undo a half-applied deploy, and
    /// the most likely reason to reach it is that the token was cancelled in the first place.
    private async Task<DeployOutcome> RollbackAsync(
        DeployPreflight preflight,
        string failure,
        IReadOnlyList<string> composeServices,
        Action<string> onStep)
    {
        string? logs = null;
        try
        {
            logs = (await _compose.LogsAsync(composeServices, 50, CancellationToken.None)).StdOut;
        }
        catch (CommandHostException)
        {
            // Losing the logs must not cost the rollback.
        }

        onStep("rolling back");
        try
        {
            await WriteEnvAsync(preflight.Env, preflight.Snapshot, backup: false, CancellationToken.None);

            // The restored tag may no longer be on the host — the image it replaced was pulled,
            // this one has to be too before compose can bring it back up.
            var pull = await _compose.PullAsync(composeServices, CancellationToken.None);
            if (!pull.Succeeded)
                return new DeployOutcome(
                    false, failure, logs, false,
                    $"the env file was restored, but compose pull failed: {pull.FailureMessage}");

            var up = await _compose.UpAsync(CancellationToken.None);
            if (!up.Succeeded)
                return new DeployOutcome(
                    false, failure, logs, false,
                    $"the env file was restored, but compose up failed: {up.FailureMessage}");
        }
        catch (Exception ex)
        {
            return new DeployOutcome(
                false, failure, logs, false,
                $"the env file may still hold the new values: {ex.Message}");
        }

        return new DeployOutcome(false, failure, logs, true, null);
    }

    private async Task WriteEnvAsync(
        EnvFile env,
        IReadOnlyDictionary<string, string?> values,
        bool backup,
        CancellationToken cancellationToken)
    {
        var path = target.EnvFilePath!;

        if (backup)
        {
            var backupPath = path + ".bak";
            await target.Host.WriteFileAsync(backupPath, env.Render(), cancellationToken);
            await target.Host.ProtectFileAsync(backupPath, cancellationToken);
        }

        foreach (var (key, value) in values)
        {
            if (value is null)
                env.Remove(key);
            else
                env.Set(key, value);
        }

        await target.Host.ReplaceFileAsync(path, env.Render(), cancellationToken);
    }

    /// Null once everything is up; otherwise the reason it never got there.
    private async Task<string?> WaitForHealthAsync(
        IReadOnlyList<string> composeServices, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        string? last = null;

        while (true)
        {
            var containers = await _compose.PsAsync(cancellationToken, includeStopped: false);
            var pending = new List<string>();

            foreach (var name in composeServices)
            {
                var matching = containers.Where(entry => entry.Name == name).ToList();

                if (matching.Count == 0)
                {
                    pending.Add($"{name}: no container");
                    continue;
                }

                // Every replica has to be settled, not just whichever compose listed first.
                foreach (var container in matching.Where(entry => !IsSettled(entry)))
                    pending.Add($"{name}: {Describe(container)}");
            }

            if (pending.Count == 0)
                return null;

            last = string.Join(", ", pending);

            if (DateTimeOffset.UtcNow >= deadline)
                return $"timed out waiting for health — {last}";

            await Task.Delay(TimeSpan.FromSeconds(3), cancellationToken);
        }
    }

    /// An image without a HEALTHCHECK reports no health at all, and there "running" is the
    /// strongest statement compose can make about it.
    private static bool IsSettled(ComposeService container) =>
        container.Health is { Length: > 0 } health
            ? health.Equals("healthy", StringComparison.OrdinalIgnoreCase)
            : container.State.Equals("running", StringComparison.OrdinalIgnoreCase);

    private static string Describe(ComposeService container) =>
        container.Health is { Length: > 0 } health
            ? $"{container.State}/{health}"
            : container.State;
}
