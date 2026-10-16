using ProjectWarehouse.Server.Infrastructure;

namespace ProjectWarehouse.Server.Models.Files;

/// <summary>
/// File metadata. <c>StorageKey</c> is deliberately absent — it is an internal storage detail.
/// </summary>
public class DataFileDto : IHasIdentity
{
    public Guid Id { get; init; }
    public string OriginalFileName { get; init; } = null!;
    public string ContentType { get; init; } = null!;
    public long SizeBytes { get; init; }
    public int? ImageWidth { get; init; }
    public int? ImageHeight { get; init; }
    public bool IsImage { get; init; }
    public Guid? CreatedById { get; init; }
    public string? CreatedByUserName { get; init; }
    public DateTime CreatedAt { get; init; }
}
