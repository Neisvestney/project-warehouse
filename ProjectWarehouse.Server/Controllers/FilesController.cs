using AutoMapper;
using AutoMapper.QueryableExtensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;
using ProjectWarehouse.Server.Data;
using ProjectWarehouse.Server.Domain;
using ProjectWarehouse.Server.Infrastructure;
using ProjectWarehouse.Server.Infrastructure.Files;
using ProjectWarehouse.Server.Models;
using ProjectWarehouse.Server.Models.Files;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

namespace ProjectWarehouse.Server.Controllers;

/// <summary>
/// File upload and delivery. There is deliberately no delete endpoint: the only way to remove a
/// file is to drop the reference to it, after which the GC collects it. That makes the state
/// "entity points at a deleted file" unreachable.
/// </summary>
[Route("api/files")]
public class FilesController(
    ApplicationDbContext db,
    IMapper mapper,
    IFileStorage storage,
    IOptions<DataFilesOptions> options,
    ILogger<FilesController> logger) : AppControllerBase
{
    /// <summary>Types the browser may render in place. Everything else is served as an attachment.</summary>
    /// <remarks>
    /// image/svg+xml is absent on purpose: an SVG is a scriptable document, and serving one inline
    /// from our own origin is stored XSS.
    /// </remarks>
    private static readonly HashSet<string> InlineContentTypes =
    [
        "image/jpeg", "image/png", "image/webp", "image/gif", "application/pdf",
    ];

    /// <summary>Previews are always WebP regardless of the source format — one output format keeps
    /// the cache layout and the response content type trivial.</summary>
    private const string ThumbnailContentType = "image/webp";

    private DataFilesOptions Options => options.Value;

    /// <summary>Upload a file.</summary>
    /// <remarks>
    /// The file exists independently of any entity and is removed by the garbage collector unless a
    /// reference to it appears within OrphanTtlHours.
    /// </remarks>
    [HttpPost]
    [Authorize]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(32 * 1024 * 1024)]
    [ProducesResponseType<DataFileDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Upload(IFormFile file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            return UnprocessableEntity("file", ErrorCode.DataFileEmpty, "File is empty.");

        if (file.Length > Options.MaxFileSizeBytes)
            return UnprocessableEntity("file", ErrorCode.DataFileTooLarge,
                $"File exceeds the maximum size of {Options.MaxFileSizeBytes} bytes.",
                new Dictionary<string, object> { ["maxBytes"] = Options.MaxFileSizeBytes });

        await using var content = file.OpenReadStream();

        var header = new byte[FileSignatures.HeaderLength];
        var headerLength = await content.ReadAtLeastAsync(header, header.Length, throwOnEndOfStream: false, ct);
        content.Position = 0;

        var declaredType = file.ContentType?.Split(';')[0].Trim() ?? "";
        if (!Options.AllowedContentTypes.Contains(declaredType) ||
            !FileSignatures.IsConsistent(FileSignatures.Detect(header.AsSpan(0, headerLength)), declaredType))
            return TypeNotAllowed();

        int? imageWidth = null, imageHeight = null;
        if (declaredType.StartsWith("image/"))
        {
            try
            {
                var info = await Image.IdentifyAsync(content, ct);
                imageWidth = info.Width;
                imageHeight = info.Height;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogInformation(ex, "Rejected an unreadable image upload of declared type {ContentType}", declaredType);
                return TypeNotAllowed();
            }

            content.Position = 0;
        }

        var id = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var storageKey = $"{now:yyyy}/{now:MM}/{now:dd}/{id}{FileSignatures.ExtensionFor(declaredType)}";

        await storage.SaveAsync(storageKey, content, ct);

        var dataFile = new DataFile
        {
            Id = id,
            StorageKey = storageKey,
            OriginalFileName = SanitizeFileName(file.FileName),
            ContentType = declaredType,
            SizeBytes = file.Length,
            ImageWidth = imageWidth,
            ImageHeight = imageHeight,
            CreatedById = GetCurrentUserId(),
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
            return UnprocessableEntity("file", ErrorCode.DataFileStorageError, "Failed to store the file.");
        }

        await db.Entry(dataFile).Reference(x => x.CreatedBy).LoadAsync(ct);
        return Ok(mapper.Map<DataFileDto>(dataFile));
    }

    /// <summary>Get file metadata.</summary>
    [HttpGet("{id:guid}")]
    [Authorize]
    [ProducesResponseType<DataFileDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var dto = await db.DataFiles
            .Where(f => f.Id == id)
            .ProjectTo<DataFileDto>(mapper.ConfigurationProvider)
            .FirstOrDefaultAsync(ct);

        return dto is null
            ? NotFound(ErrorCode.DataFileNotFound, "File not found.")
            : Ok(dto);
    }

    /// <summary>Download the original file.</summary>
    [HttpGet("{id:guid}/content")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetContent(Guid id, CancellationToken ct)
    {
        var file = await db.DataFiles.FirstOrDefaultAsync(f => f.Id == id, ct);
        if (file is null) return NotFound(ErrorCode.DataFileNotFound, "File not found.");

        var stream = await storage.OpenReadAsync(file.StorageKey, ct);
        if (stream is null)
        {
            logger.LogError("DataFile {Id} has no bytes at {StorageKey}", id, file.StorageKey);
            return NotFound(ErrorCode.DataFileNotFound, "File content is missing.");
        }

        return StreamFile(stream, file.ContentType, file.OriginalFileName, file.CreatedAt, $"{id:N}");
    }

    /// <summary>Get a downscaled preview of an image.</summary>
    /// <remarks>
    /// Only widths from ThumbnailWidths are accepted — arbitrary values would let anyone inflate the
    /// disk cache with ?width=1,2,3,… Results are cached on disk and dropped by the GC with the original.
    /// </remarks>
    [HttpGet("{id:guid}/thumbnail")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> GetThumbnail(Guid id, [FromQuery] int width, CancellationToken ct)
    {
        if (!Options.ThumbnailWidths.Contains(width))
            return UnprocessableEntity("width", ErrorCode.DataFileWidthNotAllowed,
                "Requested preview width is not allowed.",
                new Dictionary<string, object> { ["allowed"] = string.Join(", ", Options.ThumbnailWidths) });

        var file = await db.DataFiles.FirstOrDefaultAsync(f => f.Id == id, ct);
        if (file is null) return NotFound(ErrorCode.DataFileNotFound, "File not found.");

        if (!file.ContentType.StartsWith("image/"))
            return UnprocessableEntity("id", ErrorCode.DataFileNotAnImage, "File is not an image.");

        // never upscale: an original narrower than the request is already the best preview available
        if (file.ImageWidth is { } original && original <= width)
            return await GetContent(id, ct);

        var cached = await storage.OpenThumbnailAsync(file.StorageKey, width, ct);
        if (cached is not null)
            return StreamFile(cached, ThumbnailContentType, file.OriginalFileName, file.CreatedAt, $"{id:N}-w{width}");

        var source = await storage.OpenReadAsync(file.StorageKey, ct);
        if (source is null)
        {
            logger.LogError("DataFile {Id} has no bytes at {StorageKey}", id, file.StorageKey);
            return NotFound(ErrorCode.DataFileNotFound, "File content is missing.");
        }

        var rendered = new MemoryStream();
        await using (source)
        {
            try
            {
                using var image = await Image.LoadAsync(source, ct);
                image.Mutate(x => x.Resize(new ResizeOptions { Size = new Size(width, 0), Mode = ResizeMode.Max }));
                await image.SaveAsWebpAsync(rendered, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Failed to render a {Width}px preview of {Id}", width, id);
                await rendered.DisposeAsync();
                return UnprocessableEntity("id", ErrorCode.DataFileNotAnImage, "Image could not be read.");
            }
        }

        rendered.Position = 0;
        await storage.SaveThumbnailAsync(file.StorageKey, width, rendered, ct);
        rendered.Position = 0;

        return StreamFile(rendered, ThumbnailContentType, file.OriginalFileName, file.CreatedAt, $"{id:N}-w{width}");
    }

    /// <summary>
    /// Serves a stream with the caching and disposition rules every binary endpoint here shares.
    /// </summary>
    /// <remarks>
    /// Content addressed by id is immutable — replacing a file creates a new row — so the ETag can
    /// be derived from the identifier and lets the browser get a 304 without touching the disk.
    /// </remarks>
    private FileStreamResult StreamFile(
        Stream stream, string contentType, string fileName, DateTime lastModified, string etagSource)
    {
        Response.Headers["X-Content-Type-Options"] = "nosniff";

        // passing a download name is what makes ASP.NET Core emit Content-Disposition: attachment
        var downloadName = InlineContentTypes.Contains(contentType) ? null : fileName;

        return File(stream, contentType, downloadName,
            lastModified: new DateTimeOffset(lastModified, TimeSpan.Zero),
            entityTag: new EntityTagHeaderValue($"\"{etagSource}\""),
            enableRangeProcessing: true);
    }

    private ObjectResult TypeNotAllowed() =>
        UnprocessableEntity("file", ErrorCode.DataFileTypeNotAllowed,
            "File type is not allowed.",
            new Dictionary<string, object> { ["allowed"] = string.Join(", ", Options.AllowedContentTypes) });

    private static string SanitizeFileName(string? raw)
    {
        var name = Path.GetFileName(raw ?? "").Trim();
        name = new string(name.Where(c => !char.IsControl(c)).ToArray());

        if (name.Length == 0) return "file";
        if (name.Length <= 256) return name;

        // keep the extension readable when truncating an absurdly long name
        var ext = Path.GetExtension(name);
        if (ext.Length > 16) ext = "";
        return name[..(256 - ext.Length)] + ext;
    }
}
