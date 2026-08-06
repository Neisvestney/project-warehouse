namespace ProjectWarehouse.Server.Infrastructure.Files;

public class DataFilesOptions
{
    public const string SectionName = "DataFiles";

    /// <summary>Storage root on disk. In a container this is a mounted volume.</summary>
    public string StorageRoot { get; set; } = "/data/files";

    public long MaxFileSizeBytes { get; set; } = 25 * 1024 * 1024;

    public string[] AllowedContentTypes { get; set; } =
    [
        "image/jpeg", "image/png", "image/webp", "image/gif",
        "application/pdf",
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        "text/plain", "text/csv",
    ];

    /// <summary>Accepted ?width= values for thumbnails. Arbitrary widths are rejected so the cache cannot be flooded.</summary>
    public int[] ThumbnailWidths { get; set; } = [64, 128, 256, 512, 1024];

    /// <summary>
    /// How long a file may stay unreferenced before the GC takes it. This is also the hard deadline
    /// for a form left open between picking a file and saving the entity.
    /// </summary>
    public int OrphanTtlHours { get; set; } = 48;

    public string GcCron { get; set; } = "0 30 3 * * ?";

    /// <summary>Maximum deletions per GC run — bounds the transaction size.</summary>
    public int GcBatchSize { get; set; } = 500;

    /// <summary>How long the storage stats endpoint may serve a cached disk scan.</summary>
    public int StatsCacheSeconds { get; set; } = 300;
}
