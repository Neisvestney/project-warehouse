using ProjectWarehouse.Server.Domain;

namespace ProjectWarehouse.Server.Infrastructure;

/// <summary>
/// Utilities for resolving display paths for <see cref="StoragePlaceNode"/> entities.
/// </summary>
public static class StoragePlaceNodeHelper
{
    /// <summary>
    /// Builds the full breadcrumb path for a storage place node.
    /// Returns an array ordered root-first: <c>[StoragePlaceName, …parents…, NodeName]</c>.
    /// </summary>
    /// <param name="node">
    /// The target node. Must have <see cref="StoragePlaceNode.RootStoragePlace"/> loaded.
    /// </param>
    /// <param name="nodeById">
    /// A dictionary of all nodes in the same warehouse, keyed by <see cref="StoragePlaceNode.Id"/>.
    /// Used to walk up the parent chain without additional DB queries.
    /// </param>
    public static string[] BuildPath(StoragePlaceNode node, IReadOnlyDictionary<Guid, StoragePlaceNode> nodeById)
    {
        var parts = new List<string> { node.Name };
        var parentId = node.ParentNodeId;
        while (parentId.HasValue && nodeById.TryGetValue(parentId.Value, out var parent))
        {
            parts.Add(parent.Name);
            parentId = parent.ParentNodeId;
        }
        parts.Add(node.RootStoragePlace.Name);
        parts.Reverse();
        return [.. parts];
    }
}
