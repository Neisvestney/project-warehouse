using System.Formats.Tar;
using ProjectWarehouse.Ops.Infrastructure;
using ProjectWarehouse.Ops.Infrastructure.Docker;

namespace ProjectWarehouse.Ops.Services;

public sealed record TelemetryDownload(int Files, long Bytes, string Directory);

public sealed class TelemetryService(TargetContext target)
{
    public const string VolumeKey = "telemetry";

    private readonly DockerVolumes _volumes = new(target);

    public async Task<TelemetryDownload> DownloadAsync(
        string archiveDirectory,
        int? sinceDays,
        bool clean,
        IStepReporter reporter,
        CancellationToken cancellationToken)
    {
        if (!target.Config.Volumes.TryGetValue(VolumeKey, out var composeVolume))
        {
            throw new BackupException(
                $"targets.{target.Name} has no '{VolumeKey}' volume, so there is no archive to fetch.");
        }

        string volume;
        using (var resolving = reporter.Begin("resolving volume"))
        {
            volume = await _volumes.ResolveAsync(composeVolume, cancellationToken);
            resolving.Complete();
        }

        if (sinceDays is { } window
            && await _volumes.CountRecentAsync(volume, window, cancellationToken) == 0)
        {
            return new TelemetryDownload(0, 0, archiveDirectory);
        }

        // Staged as a tar rather than extracted on the fly: a transfer that dies half way then
        // leaves a scratch file behind instead of a half-populated archive the replay stack reads.
        var staging = Path.Combine(Path.GetTempPath(), $"pwops-telemetry-{Guid.NewGuid():N}.tar");

        try
        {
            var total = await _volumes.MeasureAsync(volume, sinceDays, cancellationToken);

            using (var step = reporter.Begin("downloading", total))
            await using (var file = File.Create(staging))
            {
                var result = sinceDays is { } days
                    ? await _volumes.ArchiveRecentAsync(volume, days, file, step, cancellationToken)
                    : await _volumes.ArchiveAsync(volume, file, step, cancellationToken);

                if (!result.Succeeded)
                    throw new BackupException($"Reading {volume} failed: {result.FailureMessage}");

                step.Complete();
            }

            if (new FileInfo(staging).Length == 0)
                return new TelemetryDownload(0, 0, archiveDirectory);

            if (clean && Directory.Exists(archiveDirectory))
            {
                using var clearing = reporter.Begin("clearing the local archive");
                foreach (var existing in Directory.EnumerateFiles(archiveDirectory))
                    File.Delete(existing);

                clearing.Complete();
            }

            Directory.CreateDirectory(archiveDirectory);

            using (var extracting = reporter.Begin("extracting"))
            {
                await using var archive = File.OpenRead(staging);
                TarFile.ExtractToDirectory(archive, archiveDirectory, overwriteFiles: true);
                extracting.Complete();
            }

            var files = Directory.GetFiles(archiveDirectory, "*", SearchOption.AllDirectories);
            return new TelemetryDownload(
                files.Length, files.Sum(path => new FileInfo(path).Length), archiveDirectory);
        }
        finally
        {
            if (File.Exists(staging))
                File.Delete(staging);
        }
    }
}
