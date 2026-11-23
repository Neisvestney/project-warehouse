using ProjectWarehouse.Server.Domain;
using ProjectWarehouse.Server.Infrastructure;

namespace ProjectWarehouse.Server.Models.Tags;

public class TagDto : IHasIdentity
{
    public Guid Id { get; init; }
    public string Name { get; init; } = null!;
    public TagKind Kind { get; init; }

    /// <summary>How many objects currently carry the tag — what the delete confirmation warns about.</summary>
    public int UsageCount { get; init; }
}
