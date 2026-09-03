using System.Text.Json;
using System.Text.Json.Serialization;
using ProjectWarehouse.Ops.Infrastructure;
using ProjectWarehouse.Ops.Infrastructure.Docker;

namespace ProjectWarehouse.Ops.Services;

public sealed record BackupPart(string Name, string File, long Bytes);

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

    public IReadOnlyList<string> AvailableParts()
    {
        var parts = new List<string>();

        if (target.Config.Postgres is not null)
            parts.Add(BackupManifest.DatabasePart);

        parts.AddRange(target.Config.Volumes.Keys);
        return parts;
    }

    public async Task<string> CreateAsync(
        string backupsRoot,
        IReadOnlyList<string> parts,
        IReadOnlyDictionary<string, string> versions,
        Action<string> onStep,
        IProgress<long>? progress,
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
                    onStep("pg_dump");
                    written.Add(await DumpDatabaseAsync(directory, progress, cancellationToken));
                    continue;
                }

                if (!target.Config.Volumes.TryGetValue(part, out var composeVolume))
                    throw new BackupException($"targets.{target.Name} has no volume '{part}'.");

                onStep($"volume {part}");
                written.Add(await ArchiveVolumeAsync(
                    directory, part, composeVolume, progress, cancellationToken));
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
        string directory, IProgress<long>? progress, CancellationToken cancellationToken)
    {
        var postgres = target.Config.Postgres
            ?? throw new BackupException($"targets.{target.Name} has no postgres section.");

        const string fileName = "postgres.dump";
        var path = Path.Combine(directory, fileName);

        // Custom format rather than plain SQL: pg_restore can then drop and recreate objects
        // itself, and the dump stays a single opaque file.
        var command = _compose.Command(
            "exec", "-T", postgres.Service,
            "pg_dump", "-U", postgres.User, "-F", "c", "-d", postgres.Database);

        await StreamAsync(
            path,
            file => target.Host.RunStreamingAsync(command, file, progress, cancellationToken),
            failure => $"pg_dump failed: {failure}",
            cancellationToken);

        return new BackupPart(BackupManifest.DatabasePart, fileName, new FileInfo(path).Length);
    }

    private static async Task StreamAsync(
        string path,
        Func<Stream, Task<CommandResult>> run,
        Func<string, string> describeFailure,
        CancellationToken cancellationToken)
    {
        var succeeded = false;
        string? failure = null;

        try
        {
            await using var file = File.Create(path);
            var result = await run(file);

            succeeded = result.Succeeded;
            if (!succeeded)
                failure = result.FailureMessage;
        }
        finally
        {
            if (!succeeded)
                TryDelete(path);
        }

        if (failure is not null)
            throw new BackupException(describeFailure(failure));
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
        string composeVolume,
        IProgress<long>? progress,
        CancellationToken cancellationToken)
    {
        var volume = await _volumes.ResolveAsync(composeVolume, cancellationToken);
        var fileName = $"{part}.tar";
        var path = Path.Combine(directory, fileName);

        await StreamAsync(
            path,
            file => _volumes.ArchiveAsync(volume, file, progress, cancellationToken),
            failure => $"Archiving {volume} failed: {failure}",
            cancellationToken);

        return new BackupPart(part, fileName, new FileInfo(path).Length);
    }
}
