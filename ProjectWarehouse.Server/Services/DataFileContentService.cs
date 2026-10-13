using ProjectWarehouse.Server.Domain;
using ProjectWarehouse.Server.Infrastructure.Files;
using NetVips;

namespace ProjectWarehouse.Server.Services;

public class DataFileContentService(IFileStorage storage, ILogger<DataFileContentService> logger)
    : IDataFileContentService
{
    /// <summary>Previews are always WebP regardless of the source format — one output format keeps
    /// the cache layout and the response content type trivial.</summary>
    private const string ThumbnailContentType = "image/webp";

    /// <summary>libvips defaults the thumbnail height to the width (a square box); a huge height makes the fit width-only.</summary>
    private const int UnboundedHeight = 10_000_000;

    public async Task<DataFileContent?> OpenOriginalAsync(DataFile file, CancellationToken ct)
    {
        var stream = await storage.OpenReadAsync(file.StorageKey, ct);
        if (stream is null)
        {
            logger.LogError("DataFile {Id} has no bytes at {StorageKey}", file.Id, file.StorageKey);
            return null;
        }

        return new DataFileContent(stream, file.ContentType, file.OriginalFileName, file.CreatedAt, $"{file.Id:N}");
    }

    public async Task<DataFileContent?> OpenPreviewAsync(DataFile file, int width, CancellationToken ct)
    {
        if (!file.ContentType.StartsWith("image/"))
            throw new DataFileNotAnImageException("File is not an image.");

        // never upscale: an original narrower than the request is already the best preview available
        if (file.ImageWidth is { } original && original <= width)
            return await OpenOriginalAsync(file, ct);

        var etag = $"{file.Id:N}-w{width}";

        var cached = await storage.OpenThumbnailAsync(file.StorageKey, width, ct);
        if (cached is not null)
            return new DataFileContent(cached, ThumbnailContentType, file.OriginalFileName, file.CreatedAt, etag);

        var source = await storage.OpenReadAsync(file.StorageKey, ct);
        if (source is null)
        {
            logger.LogError("DataFile {Id} has no bytes at {StorageKey}", file.Id, file.StorageKey);
            return null;
        }

        MemoryStream rendered;
        await using (source)
        {
            try
            {
                using var image = Image.ThumbnailStream(source, width, height: UnboundedHeight, size: Enums.Size.Down);
                rendered = new MemoryStream(image.WebpsaveBuffer(keep: Enums.ForeignKeep.None));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Failed to render a {Width}px preview of {Id}", width, file.Id);
                throw new DataFileNotAnImageException("Image could not be read.");
            }
        }

        rendered.Position = 0;
        await storage.SaveThumbnailAsync(file.StorageKey, width, rendered, ct);
        rendered.Position = 0;

        return new DataFileContent(rendered, ThumbnailContentType, file.OriginalFileName, file.CreatedAt, etag);
    }
}
