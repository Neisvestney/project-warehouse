using ProjectWarehouse.Server.Domain;

namespace ProjectWarehouse.Server.Services;

/// <summary>
/// One node in the recursive bundle component tree returned by
/// <see cref="ICatalogService.LoadBundleComponentsTreeAsync"/>.
/// </summary>
public record BundleComponentNode
{
    public Guid BundleComponentId { get; init; }
    public Guid ComponentId       { get; init; }
    public CatalogItemType ComponentType { get; init; }
    public int Quantity { get; init; }

    /// <summary>
    /// For Variation and ProductGroup components: the concrete items that can be selected.
    /// Each option represents one possible choice for this component slot.
    /// </summary>
    public IReadOnlyList<CatalogItem> ExpandedOptions { get; init; } = [];

    /// <summary>
    /// For nested Bundle components: the recursively resolved component tree of that bundle.
    /// </summary>
    public IReadOnlyList<BundleComponentNode> NestedComponents { get; init; } = [];
}
