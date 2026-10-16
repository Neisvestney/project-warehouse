using ProjectWarehouse.Server.Domain;

namespace ProjectWarehouse.Server.Services;

/// <summary>A stream ready to be served, with everything the response headers need.</summary>
public sealed record DataFileContent(
    Stream Stream, string ContentType, string FileName, DateTime LastModified, string ETagSource);

/// <summary>Thrown when a preview is asked of something that is not a decodable image.</summary>
public class DataFileNotAnImageException(string message) : Exception(message);

/// <summary>
/// Turns a <see cref="DataFile"/> row into bytes to serve. Every endpoint that delivers file content —
/// the files API and the user avatar — goes through here, so the preview format and the disk cache
/// have one definition.
/// </summary>
public interface IDataFileContentService
{
    /// <summary>The original bytes. Null when storage has none — the row and the disk have drifted apart.</summary>
    Task<DataFileContent?> OpenOriginalAsync(DataFile file, CancellationToken ct);

    /// <summary>
    /// A WebP preview no wider than <paramref name="width"/>, rendered once and cached on disk. An original
    /// that is already narrower is returned as-is rather than upscaled. Null when the bytes are missing.
    /// </summary>
    /// <exception cref="DataFileNotAnImageException">The file is not an image, or could not be decoded.</exception>
    Task<DataFileContent?> OpenPreviewAsync(DataFile file, int width, CancellationToken ct);
}
