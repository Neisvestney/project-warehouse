namespace ProjectWarehouse.Server.Models.System;

public class StorageStatsDto
{
    public int FileCount { get; init; }
    public long TotalSizeBytes { get; init; }
    public IReadOnlyList<ContentTypeStatDto> ByContentType { get; init; } = [];
    public IReadOnlyList<LargestFileDto> LargestFiles { get; init; } = [];

    /// <summary>Files no foreign key points at, including those still inside the orphan TTL.</summary>
    public int OrphanCount { get; init; }
    public long OrphanSizeBytes { get; init; }

    /// <summary>Subset of the above already past the TTL — what the next GC run will take.</summary>
    public int OrphanDueCount { get; init; }
    public long OrphanDueSizeBytes { get; init; }

    public long ThumbnailCacheSizeBytes { get; init; }
    public int OrphanTtlHours { get; init; }

    /// <summary>When the cached disk figures were taken. Null when computed on this request.</summary>
    public DateTime? DiskStatsAt { get; init; }

    /// <summary>Null when the mount point could not be resolved.</summary>
    public DiskSpaceDto? Disk { get; init; }
}

public class ContentTypeStatDto
{
    public string ContentType { get; init; } = null!;
    public int Count { get; init; }
    public long SizeBytes { get; init; }
}

public class LargestFileDto
{
    public Guid Id { get; init; }
    public string OriginalFileName { get; init; } = null!;
    public string ContentType { get; init; } = null!;
    public long SizeBytes { get; init; }
    public DateTime CreatedAt { get; init; }
}

public class DiskSpaceDto
{
    public string MountPoint { get; init; } = null!;
    public long TotalBytes { get; init; }
    public long FreeBytes { get; init; }
    public long UsedBytes { get; init; }
}
