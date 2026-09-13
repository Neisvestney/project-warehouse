namespace ProjectWarehouse.Server.Models.Tags;

/// <summary>Body of the dedicated tags endpoint of a document; replaces the whole tag set.</summary>
public class UpdateTagsRequest
{
    public IReadOnlyList<Guid> Tags { get; init; } = [];
}
