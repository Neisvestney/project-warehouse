using ProjectWarehouse.Server.Domain;
using ProjectWarehouse.Server.Infrastructure;
using ProjectWarehouse.Server.Models.Receipts;

namespace ProjectWarehouse.Server.Services;

public interface IInventoryService
{
    // ── Standard (count-based) items ────────────────────────────────────────

    /// <summary>
    /// Adds <paramref name="count"/> units to the StoragePlaceNodeItemsGroup for the given node/catalog item.
    /// Creates the group record if it does not yet exist.
    /// Writes a changelog entry on the node.
    /// </summary>
    Task AddStandardItemsToNodeAsync(
        Guid nodeId,
        Guid catalogItemId,
        int count,
        string action = InventoryActions.AddStandardItems,
        CancellationToken ct = default);

    /// <summary>
    /// Removes <paramref name="count"/> units from the node. Throws if count would go below zero.
    /// Writes a changelog entry on the node.
    /// </summary>
    Task RemoveStandardItemsFromNodeAsync(
        Guid nodeId,
        Guid catalogItemId,
        int count,
        string action = InventoryActions.RemoveStandardItems,
        CancellationToken ct = default);

    // ── Unit items (serialised, one per physical unit) ───────────────────────

    /// <summary>
    /// Validates inventory number uniqueness and creates a new <see cref="UnitInventoryItem"/> at the given node.
    /// Adds the entity to the context but does <b>not</b> call <c>SaveChangesAsync</c> —
    /// the caller is responsible for saving inside a transaction.
    /// Throws <see cref="InvalidOperationException"/> if the inventory number already exists for the catalog item.
    /// </summary>
    Task<UnitInventoryItem> CreateUnitItemAsync(
        Guid nodeId,
        Guid catalogItemId,
        string inventoryNumber,
        CancellationToken ct = default);

    /// <summary>
    /// Creates and places a new <see cref="UnitInventoryItem"/> at the given node.
    /// Validates inventory number uniqueness, saves, and writes a changelog entry.
    /// </summary>
    Task<UnitInventoryItem> PlaceUnitItemToNodeAsync(
        Guid nodeId,
        Guid catalogItemId,
        string inventoryNumber,
        string action = InventoryActions.AddUnitItem,
        CancellationToken ct = default);

    /// <summary>
    /// Deletes the <see cref="UnitInventoryItem"/> with the given ID.
    /// Throws <see cref="InventoryItemNodeMismatchException"/> if the item is not in <paramref name="expectedNodeId"/>.
    /// Writes a changelog entry on the node.
    /// </summary>
    Task RemoveUnitItemAsync(
        Guid unitItemId,
        Guid expectedNodeId,
        string action = InventoryActions.RemoveUnitItem,
        CancellationToken ct = default);

    // ── Assembled bundle items ───────────────────────────────────────────────

    /// <summary>
    /// Creates a new <see cref="AssembledBundleInventoryItem"/> at the given node with the specified components.
    /// For components with <c>NewUnitItem</c> set, a new <see cref="UnitInventoryItem"/> is created first
    /// via <see cref="CreateUnitItemAsync"/> before building the bundle.
    /// Writes a changelog entry on the node.
    /// </summary>
    Task<AssembledBundleInventoryItem> AddAssembledBundleToNodeAsync(
        Guid nodeId,
        Guid catalogItemId,
        IReadOnlyList<AssembledBundlePlacementComponentRequest> components,
        string action = InventoryActions.AddAssembledBundle,
        CancellationToken ct = default);

    /// <summary>
    /// Deletes the <see cref="AssembledBundleInventoryItem"/> with the given ID.
    /// Throws <see cref="InventoryItemNodeMismatchException"/> if the item is not in <paramref name="expectedNodeId"/>.
    /// Writes a changelog entry on the node.
    /// </summary>
    Task RemoveAssembledBundleAsync(
        Guid assembledBundleItemId,
        Guid expectedNodeId,
        string action = InventoryActions.RemoveAssembledBundle,
        CancellationToken ct = default);

    // ── Movement ─────────────────────────────────────────────────────────────

    Task MoveStandardItemsAsync(
        Guid fromNodeId,
        Guid toNodeId,
        Guid catalogItemId,
        int count,
        string action = InventoryActions.MoveStandardItems,
        CancellationToken ct = default);

    Task MoveUnitItemAsync(
        Guid unitItemId,
        Guid toNodeId,
        string action = InventoryActions.MoveUnitItem,
        CancellationToken ct = default);

    /// <summary>
    /// Moves an <see cref="AssembledBundleInventoryItem"/> to <paramref name="toNodeId"/>.
    /// Writes changelog entries on both the source and destination nodes.
    /// </summary>
    Task MoveAssembledBundleAsync(
        Guid assembledBundleItemId,
        Guid toNodeId,
        string action = InventoryActions.MoveAssembledBundle,
        CancellationToken ct = default);
}
