using AutoMapper;
using AutoMapper.QueryableExtensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ProjectWarehouse.Server.Data;
using ProjectWarehouse.Server.Domain;
using ProjectWarehouse.Server.Infrastructure;
using ProjectWarehouse.Server.Infrastructure.Files;
using ProjectWarehouse.Server.Models;
using ProjectWarehouse.Server.Models.Files;
using ProjectWarehouse.Server.Services;
using SixLabors.ImageSharp;

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
    IDataFileFactory dataFiles,
    IDataFileContentService fileContent,
    IOptions<DataFilesOptions> options,
    ILogger<FilesController> logger) : AppControllerBase
{
    private DataFilesOptions Options => options.Value;

    /// <summary>Upload a file.</summary>
    /// <remarks>
    /// The file exists independently of any entity and is removed by the garbage collector unless a
    /// reference to it appears within <c>DataFiles:OrphanTtlHours</c>.
    /// Body: <c>multipart/form-data</c> with a single <c>file</c> part; the request itself is capped at 32 MB.
    /// Every error is a 422 bound to the <c>file</c> field:
    /// <list type="bullet">
    ///   <item><c>dataFileEmpty</c> — no file part, or zero bytes</item>
    ///   <item><c>dataFileTooLarge</c> — over <c>DataFiles:MaxFileSizeBytes</c>; <c>args</c>: <c>maxBytes</c></item>
    ///   <item><c>dataFileTypeNotAllowed</c> — the declared content type is not in <c>DataFiles:AllowedContentTypes</c>, does not match the leading bytes, or the declared image could not be decoded; <c>args</c>: <c>allowed</c> (comma-separated list)</item>
    ///   <item><c>dataFileStorageError</c> — the bytes were written but the metadata row was not; the bytes are removed again</item>
    /// </list>
    /// Requires authentication only.
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

        DataFile dataFile;
        try
        {
            dataFile = await dataFiles.CreateAsync(content, declaredType, SanitizeFileName(file.FileName),
                file.Length, GetCurrentUserId(), imageWidth, imageHeight, ct);
        }
        catch (DataFileStorageException)
        {
            return UnprocessableEntity("file", ErrorCode.DataFileStorageError, "Failed to store the file.");
        }

        await db.Entry(dataFile).Reference(x => x.CreatedBy).LoadAsync(ct);
        return Ok(mapper.Map<DataFileDto>(dataFile));
    }

    /// <summary>Get file metadata.</summary>
    /// <remarks>
    /// Returns 404 <c>dataFileNotFound</c> — for an unknown id, and equally for one the GC already collected
    /// because no entity referenced it in time. Requires authentication only; there is no per-file access check.
    /// </remarks>
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
    /// <remarks>
    /// Returns 404 <c>dataFileNotFound</c> for an unknown id and for a row whose bytes are missing from
    /// storage (logged as an error — that state means the two halves drifted apart).
    /// Responses carry <c>X-Content-Type-Options: nosniff</c> and an id-derived ETag, and support range
    /// requests. Only <c>image/jpeg</c>, <c>image/png</c>, <c>image/webp</c>, <c>image/gif</c> and
    /// <c>application/pdf</c> are served inline; everything else gets
    /// <c>Content-Disposition: attachment</c> — notably SVG, which would otherwise be stored XSS.
    /// </remarks>
    [HttpGet("{id:guid}/content")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetContent(Guid id, CancellationToken ct)
    {
        var file = await db.DataFiles.FirstOrDefaultAsync(f => f.Id == id, ct);
        if (file is null) return NotFound(ErrorCode.DataFileNotFound, "File not found.");

        var content = await fileContent.OpenOriginalAsync(file, ct);
        return content is null
            ? NotFound(ErrorCode.DataFileNotFound, "File content is missing.")
            : StreamDataFile(content);
    }

    /// <summary>Get a downscaled preview of an image.</summary>
    /// <remarks>
    /// Only widths from <c>DataFiles:ThumbnailWidths</c> are accepted — arbitrary values would let anyone
    /// inflate the disk cache with ?width=1,2,3,… Results are cached on disk and dropped by the GC with the
    /// original. Query param: <c>width</c> (required, must be on the allow-list). Previews are always WebP,
    /// and an original no wider than the request is streamed as-is instead of being upscaled.
    /// Errors:
    /// <list type="bullet">
    ///   <item>422 <c>dataFileWidthNotAllowed</c> on <c>width</c>; <c>args</c>: <c>allowed</c> (comma-separated widths)</item>
    ///   <item>404 <c>dataFileNotFound</c> — unknown id, or the row's bytes are missing from storage</item>
    ///   <item>422 <c>dataFileNotAnImage</c> on <c>id</c> — the stored content type is not <c>image/*</c>, or the image could not be decoded</item>
    /// </list>
    /// Responses carry <c>nosniff</c> and an ETag derived from id + width, as on the content endpoint.
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

        DataFileContent? content;
        try
        {
            content = await fileContent.OpenPreviewAsync(file, width, ct);
        }
        catch (DataFileNotAnImageException ex)
        {
            return UnprocessableEntity("id", ErrorCode.DataFileNotAnImage, ex.Message);
        }

        return content is null
            ? NotFound(ErrorCode.DataFileNotFound, "File content is missing.")
            : StreamDataFile(content);
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
