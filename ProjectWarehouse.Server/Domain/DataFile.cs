using EntityFrameworkCore.Projectables;
using ProjectWarehouse.Server.Infrastructure;

namespace ProjectWarehouse.Server.Domain;

/// <summary>
/// A stored user file. Exists independently of the entities that reference it; a row nothing
/// points at is collected by <c>DataFilesGcJob</c>, so references must always be real foreign keys.
/// </summary>
public class DataFile : IHasIdentity
{
    public Guid Id { get; set; }

    /// <summary>Path inside the storage. Not the name the file was uploaded under.</summary>
    public string StorageKey { get; set; } = null!;

    /// <summary>Sanitized client-supplied name, used as the download file name.</summary>
    public string OriginalFileName { get; set; } = null!;

    public string ContentType { get; set; } = null!;
    public long SizeBytes { get; set; }

    /// <summary>Images only — lets the frontend reserve layout space before the preview loads.</summary>
    public int? ImageWidth { get; set; }
    public int? ImageHeight { get; set; }

    public Guid? CreatedById { get; set; }
    public ApplicationUser? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }

    [Projectable] public bool IsImage => ContentType.StartsWith("image/");
}
