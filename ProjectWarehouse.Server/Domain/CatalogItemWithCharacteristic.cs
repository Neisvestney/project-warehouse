using EntityFrameworkCore.Projectables;
using ProjectWarehouse.Server.Infrastructure;

namespace ProjectWarehouse.Server.Domain;

public class CatalogItemWithCharacteristic : IHasIdentity
{
    public Guid Id { get; set; }
    public string Characteristic { get; set; } = null!;
    public string? Barcode { get; set; }

    public Guid CatalogItemId { get; set; }
    public CatalogItem CatalogItem { get; set; } = null!;

    public ICollection<StoragePlaceNodeItemsGroup> StoragePlaceNodesItemsGroups { get; set; } = [];
    public ICollection<InboundOrderDeclaredItemsGroup> InboundOrderDeclaredItemsGroups { get; set; } = [];
    public ICollection<InboundOrderProcessedItemsGroup> InboundOrderProcessedItemsGroups { get; set; } = [];
    public ICollection<InboundOrderDraftItemsGroup> InboundOrderDraftItemsGroups { get; set; } = [];

    [Projectable]
    public string SearchString =>
        (CatalogItem.Name ?? "") + " " +
        (CatalogItem.Article ?? "") + " " +
        (CatalogItem.Barcode ?? "") + " " +
        (Barcode ?? "") + " " +
        (Characteristic ?? "");
}