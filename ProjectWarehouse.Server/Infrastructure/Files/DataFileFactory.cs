using ProjectWarehouse.Server.Data;
using ProjectWarehouse.Server.Domain;

namespace ProjectWarehouse.Server.Infrastructure.Files;

public class DataFileFactory(
    ApplicationDbContext db,
    IFileStorage storage,
    ILogger<DataFileFactory> logger) : IDataFileFactory
{
    public async Task<DataFile> CreateAsync(
        Stream content,
        string contentType,
        string fileName,
        long sizeBytes,
        Guid? createdById,
        int? imageWidth = null,
        int? imageHeight = null,
        CancellationToken ct = default)
    {
        var id = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var storageKey = $"{now:yyyy}/{now:MM}/{now:dd}/{id}{FileSignatures.ExtensionFor(contentType)}";

        await storage.SaveAsync(storageKey, content, ct);

        var dataFile = new DataFile
        {
            Id = id,
            StorageKey = storageKey,
            OriginalFileName = fileName,
            ContentType = contentType,
            SizeBytes = sizeBytes,
            ImageWidth = imageWidth,
            ImageHeight = imageHeight,
            CreatedById = createdById,
            CreatedAt = now,
        };

        try
        {
            db.DataFiles.Add(dataFile);
            await db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            // bytes without a row are invisible to the GC, which only scans the table — drop them now
            logger.LogError(ex, "Failed to persist metadata for {StorageKey}; removing the stored bytes", storageKey);
            await storage.DeleteAsync(storageKey, CancellationToken.None);
            throw new DataFileStorageException("Failed to store the file.", ex);
        }

        return dataFile;
    }
}
