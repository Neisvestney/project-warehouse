using Microsoft.EntityFrameworkCore;
using ProjectWarehouse.Server.Data;
using ProjectWarehouse.Server.Domain;
using ProjectWarehouse.Server.Infrastructure;
using ProjectWarehouse.Server.Models.Tags;

namespace ProjectWarehouse.Server.Services;

public class TagsService(ApplicationDbContext db) : ITagsService
{
    private static readonly TagKind[] AllKinds = Enum.GetValues<TagKind>();

    public async Task<IReadOnlyList<TagDto>> GetAllAsync(
        TagKind? kind = null, string? search = null, CancellationToken ct = default)
    {
        TagKind[] kinds = kind is null ? AllKinds : [kind.Value];
        var result = new List<TagDto>();

        foreach (var k in kinds)
        {
            result.AddRange(await Project(k)
                .WhereMatchesSearch(t => t.Name, search)
                .OrderBy(t => t.Name)
                .ToListAsync(ct));
        }

        return result;
    }

    public async Task<TagDto?> FindAsync(Guid id, CancellationToken ct = default)
    {
        var tag = await db.Tags.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id, ct);
        if (tag is null)
            return null;

        return await Project(KindOf(tag)).FirstOrDefaultAsync(t => t.Id == id, ct);
    }

    public Task<bool> NameTakenAsync(
        TagKind kind, string name, Guid? excludeId = null, CancellationToken ct = default) =>
        EntitiesOf(kind).AnyAsync(t => t.Name == name && (excludeId == null || t.Id != excludeId), ct);

    public async Task<TagDto> CreateAsync(TagKind kind, string name, CancellationToken ct = default)
    {
        var tag = NewTag(kind, name);
        db.Tags.Add(tag);
        await db.SaveChangesAsync(ct);

        return new TagDto { Id = tag.Id, Name = tag.Name, Kind = kind, UsageCount = 0 };
    }

    public async Task<TagDto?> RenameAsync(Guid id, string name, CancellationToken ct = default)
    {
        var tag = await db.Tags.FirstOrDefaultAsync(t => t.Id == id, ct);
        if (tag is null)
            return null;

        tag.Name = name;
        await db.SaveChangesAsync(ct);

        return await Project(KindOf(tag)).FirstOrDefaultAsync(t => t.Id == id, ct);
    }

    public async Task<TagDto?> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var tag = await db.Tags.FirstOrDefaultAsync(t => t.Id == id, ct);
        if (tag is null)
            return null;

        // Gone between the two reads: report it as absent rather than throwing.
        var deleted = await Project(KindOf(tag)).FirstOrDefaultAsync(t => t.Id == id, ct);
        if (deleted is null)
            return null;

        // The join rows go with it: the implicit link entities cascade from the tag side.
        db.Tags.Remove(tag);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Someone else deleted the same tag first — the outcome the caller asked for either way.
            return null;
        }

        return deleted;
    }

    // ---------- per-kind mapping: a new tag subtype adds one arm to each of the three switches ----------

    private IQueryable<TagDto> Project(TagKind kind) => kind switch
    {
        TagKind.Receipt => db.ReceiptTags.Select(t => new TagDto
        {
            Id = t.Id, Name = t.Name, Kind = TagKind.Receipt, UsageCount = t.Receipts.Count,
        }),
        TagKind.CatalogItem => db.CatalogItemTags.Select(t => new TagDto
        {
            Id = t.Id, Name = t.Name, Kind = TagKind.CatalogItem, UsageCount = t.Items.Count,
        }),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unmapped tag kind."),
    };

    private IQueryable<Tag> EntitiesOf(TagKind kind) => kind switch
    {
        TagKind.Receipt => db.ReceiptTags,
        TagKind.CatalogItem => db.CatalogItemTags,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unmapped tag kind."),
    };

    private static Tag NewTag(TagKind kind, string name) => kind switch
    {
        TagKind.Receipt => new ReceiptTag { Id = Guid.NewGuid(), Name = name },
        TagKind.CatalogItem => new CatalogItemTag { Id = Guid.NewGuid(), Name = name },
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unmapped tag kind."),
    };

    private static TagKind KindOf(Tag tag) => tag switch
    {
        ReceiptTag => TagKind.Receipt,
        CatalogItemTag => TagKind.CatalogItem,
        _ => throw new InvalidOperationException($"Unmapped tag subtype {tag.GetType().Name}."),
    };
}
