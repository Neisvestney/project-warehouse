using ProjectWarehouse.Server.Models.Catalog;

namespace ProjectWarehouse.Server.Models.Writeoffs;

public class WriteoffItemDto
{
    public Guid Id { get; init; }
    public Guid SourceNodeId { get; init; }
    public string[] SourceNodePath { get; init; } = [];
    public string? Notes { get; init; }

    // Standard item
    public Guid? CatalogItemId { get; init; }
    public CatalogItemSummaryDto? CatalogItem { get; init; }
    public int Count { get; init; }

    // Unit item
    public Guid? UnitInventoryItemId { get; init; }
    public string? InventoryNumber { get; init; }

    /// <summary>Catalog item display name. Populated for all item types.</summary>
    public string CatalogItemName { get; init; } = null!;
}
