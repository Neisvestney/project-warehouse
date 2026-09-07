using ProjectWarehouse.Server.Domain;
using ProjectWarehouse.Server.Models.Tags;

namespace ProjectWarehouse.Server.Services;

/// <summary>
/// Tag administration across every <see cref="TagKind"/>. Names are unique per kind, never globally, so
/// every lookup here takes the kind along with the name.
/// </summary>
public interface ITagsService
{
    /// <summary>All tags of the given kind (or of every kind when it is null), ordered by name.</summary>
    Task<IReadOnlyList<TagDto>> GetAllAsync(TagKind? kind = null, string? search = null, CancellationToken ct = default);

    /// <summary>Null when the tag is gone.</summary>
    Task<TagDto?> FindAsync(Guid id, CancellationToken ct = default);

    /// <summary>True when another tag of the same kind already carries the name.</summary>
    Task<bool> NameTakenAsync(TagKind kind, string name, Guid? excludeId = null, CancellationToken ct = default);

    Task<TagDto> CreateAsync(TagKind kind, string name, CancellationToken ct = default);

    /// <summary>Null when the tag is gone.</summary>
    Task<TagDto?> RenameAsync(Guid id, string name, CancellationToken ct = default);

    /// <summary>Deletes the tag and unbinds it from every object. Null when the tag was already gone.</summary>
    Task<TagDto?> DeleteAsync(Guid id, CancellationToken ct = default);
}
