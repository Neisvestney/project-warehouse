using ProjectWarehouse.Server.Infrastructure;

namespace ProjectWarehouse.Server.Domain;

/// <summary>
/// A single counted position inside a <see cref="StocktakeNode"/>. Shapes by <see cref="Kind"/>:
/// - Standard: CountedQuantity >= 0, InventoryNumber == null, UnitInventoryItemId == null
/// - Unit, known serial:   CountedQuantity in {0,1}, InventoryNumber set, UnitInventoryItemId != null
/// - Unit, unknown serial: CountedQuantity == 1,      InventoryNumber set, UnitInventoryItemId == null (surplus)
/// <see cref="CatalogItemId"/> and <see cref="InventoryNumber"/> are stored on the line itself because a
/// surplus has no <see cref="UnitInventoryItem"/> to point at yet, and the reference is SetNull.
/// </summary>
public class StocktakeItem : IHasIdentity
{
    public Guid Id { get; set; }

    public Guid StocktakeNodeId { get; set; }
    public StocktakeNode StocktakeNode { get; set; } = null!;

    public StocktakeItemKind Kind { get; set; }

    public Guid CatalogItemId { get; set; }
    public CatalogItem CatalogItem { get; set; } = null!;

    public int CountedQuantity { get; set; }

    public string? InventoryNumber { get; set; }

    public Guid? UnitInventoryItemId { get; set; }
    public UnitInventoryItem? UnitInventoryItem { get; set; }

    public string? Notes { get; set; }

    /// <summary>Stock change applied by the finish operation: positive for a surplus, negative for a
    /// shortage, zero when the count matched. Null while the document is not finished.</summary>
    public int? AppliedDelta { get; set; }
}
