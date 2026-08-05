using ProjectWarehouse.Server.Domain;
using ProjectWarehouse.Server.Infrastructure;

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

    /// <summary>
    /// Detaches the <see cref="UnitInventoryItem"/> from its node (sets <c>StoragePlaceNodeId</c> to null)
    /// without deleting the row — used when a fulfillment takes hold of the item, so its identity and
    /// any future data on the row survive the round trip. Throws <see cref="InventoryItemNodeMismatchException"/>
    /// if the item is not in <paramref name="expectedNodeId"/>. Writes a changelog entry on the node.
    /// </summary>
    Task DetachUnitItemAsync(
        Guid unitItemId,
        Guid expectedNodeId,
        string action = InventoryActions.RemoveUnitItem,
        CancellationToken ct = default);

    /// <summary>
    /// Reattaches a previously detached <see cref="UnitInventoryItem"/> to a node (sets <c>StoragePlaceNodeId</c>).
    /// Writes a changelog entry on the node.
    /// </summary>
    Task ReattachUnitItemAsync(
        Guid unitItemId,
        Guid nodeId,
        string action = InventoryActions.AddUnitItem,
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
}
