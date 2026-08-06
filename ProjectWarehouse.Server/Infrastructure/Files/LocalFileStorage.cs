using Microsoft.Extensions.Options;

namespace ProjectWarehouse.Server.Infrastructure.Files;

/// <summary>
/// Local disk storage. Originals live under {StorageRoot}/files, generated thumbnails under
/// {StorageRoot}/thumbs — the split keeps "size of the thumbnail cache" answerable with one
/// directory walk and makes dropping a file's thumbnails a single directory delete.
/// </summary>
public class LocalFileStorage(IOptions<DataFilesOptions> options, ILogger<LocalFileStorage> logger) : IFileStorage
{
    private const string ThumbnailExtension = ".webp";

    private readonly string _filesRoot = Path.GetFullPath(Path.Combine(options.Value.StorageRoot, "files"));
    private readonly string _thumbsRoot = Path.GetFullPath(Path.Combine(options.Value.StorageRoot, "thumbs"));

    public Task<Stream?> OpenReadAsync(string key, CancellationToken ct) =>
        Task.FromResult(OpenIfExists(Resolve(_filesRoot, key)));

    public async Task SaveAsync(string key, Stream content, CancellationToken ct)
    {
        var path = Resolve(_filesRoot, key);
        await WriteAtomicAsync(path, content, ct);
    }

    public Task<bool> DeleteAsync(string key, CancellationToken ct)
    {
        var path = Resolve(_filesRoot, key);
        if (!File.Exists(path)) return Task.FromResult(false);
        File.Delete(path);
        return Task.FromResult(true);
    }

    public Task<Stream?> OpenThumbnailAsync(string storageKey, int width, CancellationToken ct) =>
        Task.FromResult(OpenIfExists(ThumbnailPath(storageKey, width)));

    public async Task SaveThumbnailAsync(string storageKey, int width, Stream content, CancellationToken ct) =>
        await WriteAtomicAsync(ThumbnailPath(storageKey, width), content, ct);

    public Task DeleteThumbnailsAsync(string storageKey, CancellationToken ct)
    {
        var dir = Resolve(_thumbsRoot, StripExtension(storageKey));
        if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        return Task.CompletedTask;
    }

    public long GetThumbnailCacheSizeBytes()
    {
        if (!Directory.Exists(_thumbsRoot)) return 0;

        long total = 0;
        foreach (var path in Directory.EnumerateFiles(_thumbsRoot, "*", SearchOption.AllDirectories))
        {
            try
            {
                total += new FileInfo(path).Length;
            }
            catch (IOException)
            {
                // a thumbnail deleted mid-walk is not worth failing the whole stats request over
                logger.LogDebug("Thumbnail {Path} vanished while measuring the cache", path);
            }
        }

        return total;
    }

    private string ThumbnailPath(string storageKey, int width) =>
        Resolve(_thumbsRoot, $"{StripExtension(storageKey)}/w{width}{ThumbnailExtension}");

    private static string StripExtension(string storageKey)
    {
        var lastDot = storageKey.LastIndexOf('.');
        var lastSlash = storageKey.LastIndexOf('/');
        return lastDot > lastSlash ? storageKey[..lastDot] : storageKey;
    }

    private static Stream? OpenIfExists(string path) =>
        File.Exists(path) ? new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read) : null;

    /// <summary>
    /// Write to a temp file then move into place: two requests generating the same thumbnail
    /// concurrently must never expose a half-written file. Move is atomic on the same volume.
    /// </summary>
    private static async Task WriteAtomicAsync(string path, Stream content, CancellationToken ct)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";

        try
        {
            await using (var target = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                await content.CopyToAsync(target, ct);

            File.Move(temp, path, overwrite: true);
        }
        catch
        {
            if (File.Exists(temp)) File.Delete(temp);
            throw;
        }
    }

    /// <summary>
    /// Keys never come from the client, but the traversal guard belongs in the storage layer
    /// rather than in whoever happens to be calling it.
    /// </summary>
    private static string Resolve(string root, string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Storage key must not be empty.", nameof(key));

        var full = Path.GetFullPath(Path.Combine(root, key));
        var prefix = root.EndsWith(Path.DirectorySeparatorChar) ? root : root + Path.DirectorySeparatorChar;

        if (!full.StartsWith(prefix, StringComparison.Ordinal))
            throw new UnauthorizedAccessException($"Storage key '{key}' resolves outside the storage root.");

        return full;
    }
}
