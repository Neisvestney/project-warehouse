namespace ProjectWarehouse.Server.Infrastructure.Files;

/// <summary>
/// Byte storage behind an abstraction so an S3 implementation can replace it without touching
/// the database schema or the controllers.
/// </summary>
public interface IFileStorage
{
    /// <summary>Must return a seekable stream — File(..., enableRangeProcessing: true) requires one.</summary>
    Task<Stream?> OpenReadAsync(string key, CancellationToken ct);

    Task SaveAsync(string key, Stream content, CancellationToken ct);

    Task<bool> DeleteAsync(string key, CancellationToken ct);

    Task<Stream?> OpenThumbnailAsync(string storageKey, int width, CancellationToken ct);

    Task SaveThumbnailAsync(string storageKey, int width, Stream content, CancellationToken ct);

    Task DeleteThumbnailsAsync(string storageKey, CancellationToken ct);

    /// <summary>Total bytes of the thumbnail subtree. Walks the whole directory — callers must cache.</summary>
    long GetThumbnailCacheSizeBytes();
}
