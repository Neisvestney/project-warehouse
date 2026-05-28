using ProjectWarehouse.Server.Models.Catalog;

namespace ProjectWarehouse.Server.Models.Receipts;

public class ReceiptItemDto
{
    public Guid Id { get; init; }
    public Guid CatalogItemId { get; init; }
    public CatalogItemSummaryDto CatalogItem { get; init; } = null!;
    public int PlannedCount { get; init; }
    public int? ReceivedCount { get; init; }
    public string? Notes { get; init; }
    public IReadOnlyList<ReceiptItemPlacementDto> Placements { get; init; } = [];
}
