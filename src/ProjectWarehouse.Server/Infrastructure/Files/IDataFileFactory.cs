using ProjectWarehouse.Server.Domain;

namespace ProjectWarehouse.Server.Infrastructure.Files;

/// <summary>
/// Persists bytes plus their metadata row, rolling the bytes back if the row cannot be saved.
/// </summary>
/// <remarks>
/// Deliberately does not re-check <c>DataFilesOptions.AllowedContentTypes</c>: that list is an upload
/// policy for untrusted input, and bytes we generated ourselves (label PDFs) are not that. Callers
/// handling user uploads must validate before calling.
/// </remarks>
public interface IDataFileFactory
{
    Task<DataFile> CreateAsync(
        Stream content,
        string contentType,
        string fileName,
        long sizeBytes,
        Guid? createdById,
        int? imageWidth = null,
        int? imageHeight = null,
        CancellationToken ct = default);
}

/// <summary>Thrown when the bytes were stored but the metadata row could not be.</summary>
public class DataFileStorageException(string message, Exception inner) : Exception(message, inner);
