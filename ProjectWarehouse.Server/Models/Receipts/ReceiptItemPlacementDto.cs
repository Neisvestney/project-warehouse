namespace ProjectWarehouse.Server.Models.Receipts;

public class ReceiptItemPlacementDto
{
    public Guid Id { get; init; }
    public Guid StoragePlaceNodeId { get; init; }

    /// <summary>
    /// Full breadcrumb path from root to node: [StoragePlace, …parents…, Node].
    /// </summary>
    public string[] NodePath { get; init; } = null!;

    /// <summary>Quantity placed. Positive for Standard items; zero for Unit placements.</summary>
    public int Count { get; init; }

    /// <summary>Set for Unit item placements.</summary>
    public Guid? UnitInventoryItemId { get; init; }
    public string? InventoryNumber { get; init; }
}
