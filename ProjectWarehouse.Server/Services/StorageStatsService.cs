using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Npgsql;
using ProjectWarehouse.Server.Data;
using ProjectWarehouse.Server.Infrastructure.Files;
using ProjectWarehouse.Server.Models.System;

namespace ProjectWarehouse.Server.Services;

public class StorageStatsService(
    ApplicationDbContext db,
    IFileStorage storage,
    NpgsqlDataSource dataSource,
    IOptions<DataFilesOptions> options,
    IMemoryCache cache,
    ILogger<StorageStatsService> logger) : IStorageStatsService
{
    private const string DiskCacheKey = "storage-stats:disk";

    private record DiskStats(long ThumbnailCacheSizeBytes, DiskSpaceDto? Disk, DateTime TakenAt);

    public async Task<StorageStatsDto> GetAsync(CancellationToken ct)
    {
        var opts = options.Value;

        var byType = await db.DataFiles
            .GroupBy(f => f.ContentType)
            .Select(g => new ContentTypeStatDto
            {
                ContentType = g.Key,
                Count = g.Count(),
                SizeBytes = g.Sum(f => f.SizeBytes),
            })
            .OrderByDescending(x => x.SizeBytes)
            .ToListAsync(ct);

        var largest = await db.DataFiles
            .OrderByDescending(f => f.SizeBytes)
            .Take(10)
            .Select(f => new LargestFileDto
            {
                Id = f.Id,
                OriginalFileName = f.OriginalFileName,
                ContentType = f.ContentType,
                SizeBytes = f.SizeBytes,
                CreatedAt = f.CreatedAt,
            })
            .ToListAsync(ct);

        var orphans = await GetOrphanStatsAsync(opts, ct);
        var (thumbnailCacheSize, disk, takenAt) = GetDiskStats(opts);

        return new StorageStatsDto
        {
            FileCount = byType.Sum(x => x.Count),
            TotalSizeBytes = byType.Sum(x => x.SizeBytes),
            ByContentType = byType,
            LargestFiles = largest,
            OrphanCount = orphans.Count,
            OrphanSizeBytes = orphans.SizeBytes,
            OrphanDueCount = orphans.DueCount,
            OrphanDueSizeBytes = orphans.DueSizeBytes,
            ThumbnailCacheSizeBytes = thumbnailCacheSize,
            OrphanTtlHours = opts.OrphanTtlHours,
            DiskStatsAt = takenAt,
            Disk = disk,
        };
    }

    /// <summary>
    /// Reuses the GC's foreign-key scan so the two can never disagree about what an orphan is.
    /// Unlike the GC this reports every unreferenced file, not only those past the TTL — the cutoff
    /// is the collector's safety margin, not the definition.
    /// </summary>
    private async Task<(int Count, long SizeBytes, int DueCount, long DueSizeBytes)> GetOrphanStatsAsync(
        DataFilesOptions opts, CancellationToken ct)
    {
        var sql = $"""
            SELECT COUNT(*)::int,
                   COALESCE(SUM(f."SizeBytes"), 0)::bigint,
                   COUNT(*) FILTER (WHERE f."CreatedAt" < $1)::int,
                   COALESCE(SUM(f."SizeBytes") FILTER (WHERE f."CreatedAt" < $1), 0)::bigint
            FROM "DataFiles" f
            WHERE {DataFileReferences.BuildOrphanPredicate(db.Model, "f")}
            """;

        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, connection);
        cmd.Parameters.AddWithValue(DateTime.UtcNow.AddHours(-opts.OrphanTtlHours));

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return (0, 0, 0, 0);

        return (reader.GetInt32(0), reader.GetInt64(1), reader.GetInt32(2), reader.GetInt64(3));
    }

    /// <summary>
    /// Walking the thumbnail tree is the only expensive part of this page, so it is cached together
    /// with the drive lookup and the page shows when the figures were taken.
    /// </summary>
    private DiskStats GetDiskStats(DataFilesOptions opts)
    {
        if (cache.TryGetValue(DiskCacheKey, out DiskStats? cached) && cached is not null)
            return cached;

        var stats = new DiskStats(storage.GetThumbnailCacheSizeBytes(), ResolveDisk(opts.StorageRoot), DateTime.UtcNow);
        cache.Set(DiskCacheKey, stats, TimeSpan.FromSeconds(opts.StatsCacheSeconds));
        return stats;
    }

    /// <summary>
    /// Picks the mount point with the longest matching prefix. Path.GetPathRoot would return "/" on
    /// Linux and report free space on the container's root filesystem rather than on the volume
    /// mounted at StorageRoot — a plausible-looking but wrong number.
    /// </summary>
    private DiskSpaceDto? ResolveDisk(string storageRoot)
    {
        try
        {
            var full = Path.GetFullPath(storageRoot);
            var drive = DriveInfo.GetDrives()
                .Where(d => SafeIsReady(d) && full.StartsWith(d.RootDirectory.FullName, StringComparison.Ordinal))
                .OrderByDescending(d => d.RootDirectory.FullName.Length)
                .FirstOrDefault();

            if (drive is null) return null;

            return new DiskSpaceDto
            {
                MountPoint = drive.RootDirectory.FullName,
                TotalBytes = drive.TotalSize,
                FreeBytes = drive.AvailableFreeSpace,
                UsedBytes = drive.TotalSize - drive.AvailableFreeSpace,
            };
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(ex, "Could not resolve disk usage for {StorageRoot}", storageRoot);
            return null;
        }
    }

    // in a container GetDrives() enumerates overlay/tmpfs/shm and individual entries can throw on access
    private static bool SafeIsReady(DriveInfo drive)
    {
        try
        {
            return drive.IsReady;
        }
        catch
        {
            return false;
        }
    }
}
