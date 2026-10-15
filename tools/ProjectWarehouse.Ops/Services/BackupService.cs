using System.Text.Json;
using System.Text.Json.Serialization;
using ProjectWarehouse.Ops.Configuration;
using ProjectWarehouse.Ops.Infrastructure;
using ProjectWarehouse.Ops.Infrastructure.Docker;

namespace ProjectWarehouse.Ops.Services;

/// <param name="Sha256">Null in a backup taken before parts were hashed.</param>
public sealed record BackupPart(string Name, string File, long Bytes, string? Sha256 = null);

public sealed record BackupManifest(
    string Target,
    DateTimeOffset TakenAt,
    IReadOnlyDictionary<string, string> Versions,
    IReadOnlyList<BackupPart> Parts)
{
    public const string FileName = "manifest.json";

    public const string DatabasePart = "db";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static async Task WriteAsync(string directory, BackupManifest manifest, CancellationToken ct)
    {
        var path = Path.Combine(directory, FileName);
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(manifest, JsonOptions), ct);
    }

    public static async Task<BackupManifest> ReadAsync(string directory, CancellationToken ct)
    {
        var path = Path.Combine(directory, FileName);
        if (!File.Exists(path))
            throw new BackupException($"{path} is missing — this is not a pwops backup.");

        return JsonSerializer.Deserialize<BackupManifest>(await File.ReadAllTextAsync(path, ct), JsonOptions)
            ?? throw new BackupException($"{path} is empty.");
    }
}

public sealed class BackupException(string message) : Exception(message);

public sealed class BackupService(TargetContext target)
{
    private readonly ComposeClient _compose = new(target);
    private readonly DockerVolumes _volumes = new(target);

    /// The telemetry volume is deliberately absent: `pwops telemetry` fetches it with an age
    /// filter and extracts it for the replay stack, and a rotated archive of that size in every
    /// backup would cost more than the data is worth restoring.
    public IReadOnlyList<string> AvailableParts()
    {
        var parts = new List<string>();

        if (target.Config.Postgres is not null)
            parts.Add(BackupManifest.DatabasePart);

        parts.AddRange(target.Config.Volumes.Keys.Where(
            key => !key.Equals(TelemetryService.VolumeKey, StringComparison.OrdinalIgnoreCase)));

        return parts;
    }

    public async Task<string> CreateAsync(
        string backupsRoot,
        IReadOnlyList<string> parts,
        IReadOnlyDictionary<string, string> versions,
        IStepReporter reporter,
        CancellationToken cancellationToken)
    {
        var takenAt = DateTimeOffset.Now;
        var directory = Path.Combine(
            backupsRoot, $"{target.Name}-{takenAt:yyyy-MM-dd'T'HH-mm-ss}");

        Directory.CreateDirectory(directory);

        var written = new List<BackupPart>();
        var complete = false;

        try
        {
            foreach (var part in parts)
            {
                if (part == BackupManifest.DatabasePart)
                {
                    written.Add(await DumpDatabaseAsync(directory, reporter, cancellationToken));
                    continue;
                }

                if (!target.Config.Volumes.TryGetValue(part, out var source))
                    throw new BackupException($"targets.{target.Name} has no volume '{part}'.");

                written.Add(await ArchiveVolumeAsync(
                    directory, part, source, reporter, cancellationToken));
            }

            await BackupManifest.WriteAsync(
                directory,
                new BackupManifest(target.Name, takenAt, versions, written),
                cancellationToken);

            complete = true;
        }
        finally
        {
            // A half-written backup is a partial copy of production data with nothing to say so.
            // Cancellation lands here too, which is the case that used to leave it behind.
            if (!complete)
                TryDelete(directory);
        }

        return directory;
    }

    private async Task<BackupPart> DumpDatabaseAsync(
        string directory, IStepReporter reporter, CancellationToken cancellationToken)
    {
        var postgres = target.Config.Postgres
            ?? throw new BackupException($"targets.{target.Name} has no postgres section.");

        const string fileName = "postgres.dump";
        var path = Path.Combine(directory, fileName);

        // Custom format rather than plain SQL: pg_restore can then drop and recreate objects
        // itself, and the dump stays a single opaque file. Bash because the image's sh is dash,
        // which has no pipefail.
        var command = _compose.Command(
            "exec", "-T", postgres.Service,
            "bash", "-c", "set -o pipefail; " + StreamChecksum.Wrap("pg_dump \"$@\""), "pg_dump",
            "-U", postgres.User, "-F", "c", "-d", postgres.Database);

        // No size to ask for: the dump is compressed as it is produced, so how large it will be is
        // only known once it is done.
        using var step = reporter.Begin("pg_dump");

        var sha256 = await StreamAsync(
            path,
            async file =>
            {
                var result = await target.Host.RunStreamingAsync(command, file, step, cancellationToken);
                if (result.Succeeded)
                    step.Complete();

                return result;
            },
            failure => $"pg_dump failed: {failure}",
            reporter,
            cancellationToken);

        return new BackupPart(BackupManifest.DatabasePart, fileName, new FileInfo(path).Length, sha256);
    }

    /// <returns>The SHA-256 the target reported, already matched against the file on disk.</returns>
    private static async Task<string> StreamAsync(
        string path,
        Func<Stream, Task<CommandResult>> run,
        Func<string, string> describeFailure,
        IStepReporter reporter,
        CancellationToken cancellationToken)
    {
        string? sha256 = null;
        string? failure = null;
        var verified = false;

        try
        {
            await using (var file = File.Create(path))
            {
                var result = await run(file);
                var (reported, stdErr) = StreamChecksum.Split(result.StdErr);

                if (!result.Succeeded)
                    failure = (result with { StdErr = stdErr }).FailureMessage;
                else if (reported is null)
                    failure = "the target reported no SHA-256 for the stream";
                else
                    sha256 = reported;
            }

            // A zero exit does not prove every byte reached this file.
            if (sha256 is not null)
            {
                var length = new FileInfo(path).Length;
                using var verifying = reporter.Begin($"verifying {Path.GetFileName(path)}", length);

                verified = StreamChecksum.Matches(
                    await StreamChecksum.OfFileAsync(path, verifying, cancellationToken), sha256);

                if (verified)
                    verifying.Complete();
                else
                    failure = $"the {length} bytes received do not match the SHA-256 the target computed";
            }
        }
        finally
        {
            if (!verified)
                TryDelete(path);
        }

        return failure is null ? sha256! : throw new BackupException(describeFailure(failure));
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
            else if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
        }
        catch (IOException)
        {
            // Nothing useful to do; the caller is already unwinding.
        }
    }

    private async Task<BackupPart> ArchiveVolumeAsync(
        string directory,
        string part,
        VolumeSource source,
        IStepReporter reporter,
        CancellationToken cancellationToken)
    {
        var volume = await _volumes.ResolveAsync(source, cancellationToken);
        var fileName = $"{part}.tar";
        var path = Path.Combine(directory, fileName);

        var contents = await _volumes.MeasureAsync(volume, VolumeFilter.All, cancellationToken);
        using var step = reporter.Begin($"volume {part}", contents?.BarTotal);

        var sha256 = await StreamAsync(
            path,
            async file =>
            {
                var result = await _volumes.ArchiveAsync(
                    volume, VolumeFilter.All, file, step, cancellationToken);
                if (result.Succeeded)
                    step.Complete();

                return result;
            },
            failure => $"Archiving {volume} failed: {failure}",
            reporter,
            cancellationToken);

        return new BackupPart(part, fileName, new FileInfo(path).Length, sha256);
    }
}
