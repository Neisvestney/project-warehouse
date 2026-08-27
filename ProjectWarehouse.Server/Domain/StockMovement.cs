using Microsoft.EntityFrameworkCore;

namespace ProjectWarehouse.Server.Domain;

/// <summary>
/// One stock delta at one node, written by <c>InventoryService</c> in the same transaction as the
/// change itself. This is the source of truth for "what came in / went out per day" — the changelog
/// records the same events, but only as jsonb diffs of a whole node, which cannot be aggregated.
/// </summary>
[Index(nameof(CreatedAt))]
[Index(nameof(CatalogItemId), nameof(CreatedAt))]
[Index(nameof(WarehouseId), nameof(CreatedAt))]
[Index(nameof(UserId), nameof(CreatedAt))]
public class StockMovement
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }

    public StockMovementDirection Direction { get; set; }

    /// <summary>Action constant of the operation that produced the row (<c>InventoryActions</c>, <c>TransferActions</c>, …).</summary>
    public string Action { get; set; } = null!;

    /// <summary>Always positive — the sign lives in <see cref="Direction"/>.</summary>
    public int Quantity { get; set; }

    public Guid CatalogItemId { get; set; }
    public CatalogItem CatalogItem { get; set; } = null!;

    // Location is denormalized down to the warehouse so a report does not have to join through a node
    // that may since have been deleted. All three are audit references: a delete nulls them out rather
    // than erasing the movement.
    public Guid? StoragePlaceNodeId { get; set; }
    public StoragePlaceNode? StoragePlaceNode { get; set; }

    public Guid? StoragePlaceId { get; set; }
    public StoragePlace? StoragePlace { get; set; }

    public Guid? WarehouseId { get; set; }
    public Warehouse? Warehouse { get; set; }

    /// <summary>Set only for movements of a unit item; null for standard quantity movements.</summary>
    public Guid? UnitInventoryItemId { get; set; }
    public UnitInventoryItem? UnitInventoryItem { get; set; }

    /// <summary>
    /// Denormalized copy of the item's number, written together with <see cref="UnitInventoryItemId"/>.
    /// Survives the item itself, so a movement of a piece that was later removed is still identifiable.
    /// </summary>
    public string? UnitInventoryNumber { get; set; }

    public Guid? UserId { get; set; }
    public ApplicationUser? User { get; set; }
}
