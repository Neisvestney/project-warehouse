using ProjectWarehouse.Server.Domain;
using ProjectWarehouse.Server.Models.Catalog;

namespace ProjectWarehouse.Server.Services;

public interface ICatalogService
{
    /// <summary>
    /// Recursively loads the component tree for a bundle, populating
    /// <paramref name="localCache"/> with all encountered catalog items.
    /// The cache is shared across calls so multiple bundles can be resolved
    /// without redundant DB queries (e.g. for order assembly or stock calculations).
    /// </summary>
    Task<IReadOnlyList<BundleComponentNode>> LoadBundleComponentsTreeAsync(
        Guid bundleId,
        Dictionary<Guid, CatalogItem> localCache,
        CancellationToken ct = default);

    /// <summary>
    /// Synchronises AssembledBundle catalog items for <paramref name="bundleId"/>:
    /// creates missing combinations, unarchives re-appearing ones, renames all,
    /// and archives combinations that no longer exist.
    /// </summary>
    Task SyncAssembledBundlesForBundleAsync(Guid bundleId, CancellationToken ct = default);

    /// <summary>
    /// Finds every Bundle that directly or indirectly contains
    /// <paramref name="componentId"/> and calls
    /// <see cref="SyncAssembledBundlesForBundleAsync"/> for each of them.
    /// Used when a Variation, ProductGroup, or nested Bundle is modified so that
    /// all ancestor Bundles get their AssembledBundle catalog items re-synced.
    /// </summary>
    Task SyncParentBundlesAsync(Guid componentId, CancellationToken ct = default);

    /// <summary>
    /// Checks whether any fields that affect assembled bundle names or descriptions have changed
    /// and, if so, regenerates them for all AssembledBundles containing <paramref name="componentId"/>.
    /// Encapsulates the field-comparison logic so callers don't need to know which fields are relevant.
    /// </summary>
    Task UpdateAssembledBundlesOnComponentChangeAsync(
        Guid componentId,
        CatalogItemDto before,
        string newName,
        string newArticle,
        string? newBarcode,
        CancellationToken ct = default);

    /// <summary>
    /// Unconditionally regenerates the Name and Description of every AssembledBundle
    /// that contains <paramref name="changedComponentId"/> as a component.
    /// </summary>
    Task UpdateAssembledBundleNamesForComponentAsync(Guid changedComponentId, CancellationToken ct = default);
}
