using System.Formats.Tar;
using System.IO.Compression;
using ProjectWarehouse.Ops.Infrastructure;
using ProjectWarehouse.Ops.Infrastructure.Docker;

namespace ProjectWarehouse.Ops.Services;

/// <param name="Bytes">What landed in the archive.</param>
/// <param name="Transferred">What crossed the wire to put it there, compressed.</param>
/// <param name="Warnings">What the fetch could not tidy up after itself.</param>
public sealed record TelemetryDownload(
    int Files,
    long Bytes,
    long Transferred,
    string Directory,
    IReadOnlyList<string> Warnings);

/// <param name="Rotated">
/// Include the rotation backups as well as the file each signal is currently being written to.
/// </param>
/// <param name="SinceDays">
/// Only files modified within this many days. The file a signal is being written to is always
/// recent, so this narrows the rotation backups and nothing else.
/// </param>
/// <param name="Clean">
/// Drop the local copies of the selected signals first, so the replay stack shows this fetch alone.
/// </param>
public sealed record TelemetryRequest(
    IReadOnlyList<string> Signals, bool Rotated, int? SinceDays, bool Clean);

/// The collector runs one file exporter per signal, so a signal is a file-name prefix in the volume
/// and picking signals is picking file names.
public static class TelemetrySignals
{
    public static readonly IReadOnlyList<string> All = ["traces", "metrics", "logs"];

    /// <param name="rotated">A rotation backup keeps the prefix and has a timestamp appended.</param>
    public static IReadOnlyList<string> Patterns(IEnumerable<string> signals, bool rotated) =>
        [.. signals.Select(signal => rotated ? $"{signal}*" : $"{signal}.json")];
}

public sealed class TelemetryService(TargetContext target)
{
    public const string VolumeKey = "telemetry";

    private const string StagedName = "telemetry.tar.gz";

    private readonly DockerVolumes _volumes = new(target);

    public async Task<TelemetryDownload> DownloadAsync(
        string archiveDirectory,
        TelemetryRequest request,
        IStepReporter reporter,
        CancellationToken cancellationToken)
    {
        if (!target.Config.Volumes.TryGetValue(VolumeKey, out var source))
        {
            throw new BackupException(
                $"targets.{target.Name} has no '{VolumeKey}' volume, so there is no archive to fetch.");
        }

        var patterns = TelemetrySignals.Patterns(request.Signals, request.Rotated);
        var filter = new VolumeFilter(request.SinceDays, patterns);

        string volume;
        using (var resolving = reporter.Begin("resolving volume"))
        {
            volume = await _volumes.ResolveAsync(source, cancellationToken);
            resolving.Complete();
        }

        // Measured before anything moves because busybox tar fails outright on an empty file list,
        // and a signal that has written nothing in the window is an answer rather than an error.
        var contents = await _volumes.MeasureAsync(volume, filter, cancellationToken);
        if (contents is { Files: 0 })
            return Nothing(archiveDirectory);

        var warnings = new List<string>();
        var remote = await target.Host.CreateTempDirectoryAsync(cancellationToken);

        // Staged locally as well as remotely: a transfer that dies half way then leaves a scratch
        // file behind instead of a half-populated archive the replay stack reads.
        var scratch = Path.Combine(Path.GetTempPath(), $"pwops-telemetry-{Guid.NewGuid():N}.tar.gz");

        try
        {
            long transferred;

            // Compressed into a file on the target first, so the transfer that follows knows how
            // many bytes it is moving. Gzip still runs exactly once.
            using (var packing = reporter.Begin("compressing"))
            {
                transferred = await _volumes.StageAsync(
                    volume, filter, remote, StagedName, cancellationToken);

                packing.Complete();
            }

            if (transferred == 0)
                return new TelemetryDownload(0, 0, 0, archiveDirectory, warnings);

            using (var step = reporter.Begin("downloading", transferred))
            await using (var file = File.Create(scratch))
            {
                var result = await _volumes.FetchStagedAsync(
                    remote, StagedName, file, step, cancellationToken);

                if (!result.Succeeded)
                    throw new BackupException($"Reading {volume} failed: {result.FailureMessage}");

                step.Complete();
            }

            if (request.Clean && Directory.Exists(archiveDirectory))
            {
                using var clearing = reporter.Begin("clearing the local archive");

                // The whole of each chosen signal, not just the files being replaced: the replay
                // stack reads every file in the directory, so a rotation left behind by a fetch
                // that skipped it would still turn up in the dashboard.
                Clear(archiveDirectory, TelemetrySignals.Patterns(request.Signals, rotated: true));
                clearing.Complete();
            }

            Directory.CreateDirectory(archiveDirectory);

            using (var extracting = reporter.Begin("extracting"))
            {
                await using var archive = File.OpenRead(scratch);
                await using var expanding = new GZipStream(archive, CompressionMode.Decompress);
                TarFile.ExtractToDirectory(expanding, archiveDirectory, overwriteFiles: true);
                extracting.Complete();
            }

            if (contents is { } measured)
                return new TelemetryDownload(
                    measured.Files, measured.Bytes, transferred, archiveDirectory, warnings);

            // The measure failed where the transfer did not, so the tally comes off what landed.
            var extracted = Select(archiveDirectory, patterns);

            return new TelemetryDownload(
                extracted.Count,
                extracted.Sum(path => new FileInfo(path).Length),
                transferred,
                archiveDirectory,
                warnings);
        }
        finally
        {
            if (File.Exists(scratch))
                File.Delete(scratch);

            await CleanAsync(remote, warnings);
        }
    }

    private static TelemetryDownload Nothing(string archiveDirectory) =>
        new(0, 0, 0, archiveDirectory, []);

    /// Reported rather than thrown: the archive is already on disk by this point, and a scratch
    /// file left on the target is worth a line, not a failed fetch.
    private async Task CleanAsync(string directory, List<string> warnings)
    {
        try
        {
            await target.Host.RemoveDirectoryAsync(directory, CancellationToken.None);
        }
        catch (CommandHostException ex)
        {
            warnings.Add($"{directory} still holds the compressed archive on the target: {ex.Message}");
        }
    }

    /// Only the signals being fetched, so a partial fetch cannot throw away another signal's copy —
    /// or the archive directory's own .gitignore.
    private static void Clear(string archiveDirectory, IReadOnlyList<string> patterns)
    {
        foreach (var path in Select(archiveDirectory, patterns))
            File.Delete(path);
    }

    /// The volume is flat, so the top level is the whole of it.
    private static List<string> Select(string archiveDirectory, IReadOnlyList<string> patterns) =>
        [.. patterns.SelectMany(pattern => Directory.EnumerateFiles(archiveDirectory, pattern))];
}
