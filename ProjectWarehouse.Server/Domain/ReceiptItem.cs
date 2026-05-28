using ProjectWarehouse.Server.Infrastructure;

namespace ProjectWarehouse.Server.Domain;

public class ReceiptItem : IHasIdentity
{
    public Guid Id { get; set; }

    public Guid ReceiptId { get; set; }
    public Receipt Receipt { get; set; } = null!;

    public Guid CatalogItemId { get; set; }
    public CatalogItem CatalogItem { get; set; } = null!;

    public int PlannedCount { get; set; }

    /// <summary>Actual count verified during the Processing phase.</summary>
    public int? ReceivedCount { get; set; }

    public string? Notes { get; set; }

    public ICollection<ReceiptItemPlacement> Placements { get; set; } = [];
}
