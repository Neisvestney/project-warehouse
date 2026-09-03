using ProjectWarehouse.Ops.Infrastructure;
using ProjectWarehouse.Ops.Infrastructure.Docker;

namespace ProjectWarehouse.Ops.Services;

public sealed record RestoreOutcome(bool Succeeded, string? Failure, IReadOnlyList<string> Warnings);

public sealed class RestoreService(TargetContext target)
{
    private readonly ComposeClient _compose = new(target);
    private readonly DockerVolumes _volumes = new(target);

    /// <param name="appServices">
    /// Compose services stopped for the duration. Postgres stays up — the restore talks to it.
    /// </param>
    public async Task<RestoreOutcome> ExecuteAsync(
        string backupDirectory,
        BackupManifest manifest,
        IReadOnlyList<string> parts,
        IReadOnlyList<string> appServices,
        Action<string> onStep,
        IProgress<long>? progress,
        CancellationToken cancellationToken)
    {
        var warnings = new List<string>();

        // Everything that can be checked without touching the target is checked here: past the
        // stop, a refusal costs an outage rather than an error message.
        var planned = Plan(backupDirectory, manifest, parts);
        var volumes = await ResolveVolumesAsync(planned, cancellationToken);
        var toStop = await ServicesToStopAsync(appServices, volumes, cancellationToken);

        var staging = await target.Host.CreateTempDirectoryAsync(cancellationToken);
        var stopped = false;

        try
        {
            // Uploads finish before anything is destroyed, so a transfer that dies mid-way costs
            // nothing but the time spent on it.
            foreach (var (part, local) in planned)
            {
                onStep($"uploading {part.File}");
                await target.Host.UploadFileAsync(
                    local, Remote(staging, part.File), progress, cancellationToken);
            }

            onStep($"stopping {string.Join(", ", toStop)}");
            var stop = await _compose.StopAsync(toStop, cancellationToken);
            if (!stop.Succeeded)
                throw new BackupException($"compose stop failed: {stop.FailureMessage}");

            stopped = true;

            foreach (var (part, _) in planned)
            {
                if (part.Name == BackupManifest.DatabasePart)
                {
                    onStep("pg_restore");
                    await RestoreDatabaseAsync(staging, part.File, cancellationToken);
                }
                else
                {
                    onStep($"volume {part.Name}");
                    await _volumes.RestoreAsync(volumes[part.Name], staging, part.File, cancellationToken);
                }
            }
        }
        catch (Exception ex) when (ex is BackupException or CommandHostException or OperationCanceledException)
        {
            await BringUpAsync(stopped, onStep, warnings);
            await CleanStagingAsync(staging, warnings);

            return new RestoreOutcome(
                false,
                ex is OperationCanceledException ? "cancelled" : ex.Message,
                warnings);
        }

        // The stack has to come back up whichever way the restore ended, so this is not in the
        // success path by accident — it is the same call the failure path makes.
        await BringUpAsync(stopped, onStep, warnings);
        await CleanStagingAsync(staging, warnings);

        return new RestoreOutcome(true, null, warnings);
    }

    private static string Remote(string staging, string fileName) =>
        $"{staging.TrimEnd('/')}/{fileName}";

    private static List<(BackupPart Part, string LocalPath)> Plan(
        string backupDirectory, BackupManifest manifest, IReadOnlyList<string> parts)
    {
        var planned = new List<(BackupPart, string)>();

        foreach (var name in parts)
        {
            var part = manifest.Parts.FirstOrDefault(entry => entry.Name == name)
                ?? throw new BackupException($"The backup holds no '{name}' part.");

            var local = Path.Combine(backupDirectory, part.File);
            if (!File.Exists(local))
                throw new BackupException($"{local} is missing.");

            // The manifest recorded the size; a file that no longer matches it was truncated
            // somewhere between then and now, and must not reach the destructive path.
            var actual = new FileInfo(local).Length;
            if (actual != part.Bytes)
            {
                throw new BackupException(
                    $"{local} is {actual} bytes, the manifest says {part.Bytes}. The backup is incomplete.");
            }

            planned.Add((part, local));
        }

        return planned;
    }

    private async Task<Dictionary<string, string>> ResolveVolumesAsync(
        IReadOnlyList<(BackupPart Part, string LocalPath)> planned, CancellationToken cancellationToken)
    {
        var volumes = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var (part, _) in planned)
        {
            if (part.Name == BackupManifest.DatabasePart)
                continue;

            if (!target.Config.Volumes.TryGetValue(part.Name, out var composeVolume))
                throw new BackupException($"targets.{target.Name} has no volume '{part.Name}'.");

            volumes[part.Name] = await _volumes.ResolveAsync(composeVolume, cancellationToken);
        }

        return volumes;
    }

    /// The services named on the target plus everyone else holding a volume being restored.
    /// A volume the tool cannot free is refused here, while refusing still costs nothing.
    private async Task<List<string>> ServicesToStopAsync(
        IReadOnlyList<string> appServices,
        IReadOnlyDictionary<string, string> volumes,
        CancellationToken cancellationToken)
    {
        var services = new HashSet<string>(appServices, StringComparer.Ordinal);
        var postgres = target.Config.Postgres?.Service;

        foreach (var (part, volume) in volumes)
        {
            foreach (var user in await _volumes.UsersAsync(volume, cancellationToken))
            {
                if (user.ComposeService is not { } service)
                {
                    throw new BackupException(
                        $"Container '{user.Container}' has {volume} mounted and is not part of this "
                            + "compose project, so it cannot be stopped. Stop it and try again.");
                }

                if (service == postgres)
                {
                    throw new BackupException(
                        $"'{part}' lives in {volume}, which postgres has mounted. Postgres has to stay "
                            + "up for the database restore, so this volume cannot be restored here — "
                            + "restore the database from its dump instead.");
                }

                services.Add(service);
            }
        }

        return [.. services];
    }

    private async Task BringUpAsync(bool stopped, Action<string> onStep, List<string> warnings)
    {
        if (!stopped)
            return;

        onStep("starting services");
        try
        {
            var up = await _compose.UpAsync(CancellationToken.None);
            if (!up.Succeeded)
                warnings.Add($"the stack is still down — compose up failed: {up.FailureMessage}");
        }
        catch (CommandHostException ex)
        {
            warnings.Add($"the stack is still down — compose up failed: {ex.Message}");
        }
    }

    private async Task CleanStagingAsync(string staging, List<string> warnings)
    {
        try
        {
            await target.Host.RemoveDirectoryAsync(staging, CancellationToken.None);
        }
        catch (CommandHostException ex)
        {
            warnings.Add($"{staging} still holds the uploaded archives on the target: {ex.Message}");
        }
    }

    private async Task RestoreDatabaseAsync(
        string staging, string fileName, CancellationToken cancellationToken)
    {
        var postgres = target.Config.Postgres
            ?? throw new BackupException($"targets.{target.Name} has no postgres section.");

        const string inContainer = "/tmp/pwops-restore.dump";

        var copy = await _compose.CopyAsync(
            Remote(staging, fileName), $"{postgres.Service}:{inContainer}", cancellationToken);

        if (!copy.Succeeded)
            throw new BackupException($"compose cp failed: {copy.FailureMessage}");

        try
        {
            // --clean --if-exists so the restore replaces the schema instead of colliding with it;
            // --single-transaction so a failure leaves the database as it was rather than half-loaded.
            var restore = await target.Host.RunAsync(
                _compose.Command(
                    "exec", "-T", postgres.Service,
                    "pg_restore", "-U", postgres.User, "-d", postgres.Database,
                    "--clean", "--if-exists", "--single-transaction", "--no-owner",
                    inContainer),
                cancellationToken);

            if (!restore.Succeeded)
                throw new BackupException($"pg_restore failed: {restore.FailureMessage}");
        }
        finally
        {
            // A copy of the whole database otherwise lives in the container until it is recreated.
            await target.Host.RunAsync(
                _compose.Command("exec", "-T", postgres.Service, "rm", "-f", inContainer),
                CancellationToken.None);
        }
    }
}
