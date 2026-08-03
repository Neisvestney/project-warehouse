using ProjectWarehouse.Server.Domain;
using ProjectWarehouse.Server.Infrastructure;

namespace ProjectWarehouse.Server.Services;

public interface ICatalogService
{
    /// <summary>
    /// Validates that saving <paramref name="rootId"/> (of type <paramref name="rootType"/>,
    /// either Bundle or Variation) with the given component/member IDs would not introduce a
    /// circular dependency.
    /// </summary>
    /// <remarks>
    /// Bundle cannot directly contain Bundle, and Variation cannot directly contain Variation —
    /// so the only two "internal" (non-leaf) node types are Bundle and Variation, connected by
    /// two edge kinds: Bundle → Variation (a bundle component of type Variation) and
    /// Variation → Bundle (a variation member of type Bundle). This walks that graph with a
    /// recursion-stack DFS and throws <see cref="BundleCircularDependencyException"/> if a node
    /// already on the current path is revisited.
    /// </remarks>
    /// <param name="rootId">The catalog item currently being saved.</param>
    /// <param name="rootType">Its type — must be Bundle or Variation.</param>
    /// <param name="rootEdgeIds">
    /// The IDs it will reference after this save (bundle component IDs, or variation member
    /// IDs) — i.e. the just-submitted request list, not yet persisted.
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    Task EnsureNoCycleAsync(
        Guid rootId,
        CatalogItemType rootType,
        IReadOnlyList<Guid> rootEdgeIds,
        CancellationToken ct = default);

    /// <summary>
    /// For each of <paramref name="catalogItemIds"/>, determines whether the catalog item is
    /// itself of type Unit, or (for Bundle/Variation) whether its composition resolves to a
    /// Unit item anywhere in its nested tree. Standard/ProductGroup items always resolve to
    /// <c>false</c>. Results are memoized across the whole call, since "contains a unit" is a
    /// property of the catalog item's composition graph, not of any particular caller/task.
    /// </summary>
    Task<Dictionary<Guid, bool>> ComputeContainsUnitAsync(
        IReadOnlyCollection<Guid> catalogItemIds,
        CancellationToken ct = default);
}
