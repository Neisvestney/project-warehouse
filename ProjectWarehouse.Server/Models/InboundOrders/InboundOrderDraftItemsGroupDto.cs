using ProjectWarehouse.Server.Infrastructure;
using ProjectWarehouse.Server.Models.Warehouses;

namespace ProjectWarehouse.Server.Models.InboundOrders;

public class InboundOrderDraftItemsGroupDto : IHasIdentity
{
    public Guid Id { get; init; }
    public string Name { get; init; } = null!;
    public string Article { get; init; } = null!;
    public string? Barcode { get; init; }
    public string? RootBarcode { get; init; }
    public string Characteristic { get; init; } = null!;
    public int Count { get; init; }
    public NodeCatalogItemDto? CatalogItem { get; init; }
    public NodeCharacteristicDto? CatalogItemWithCharacteristic { get; init; }
    public bool CreateNew { get; init; }
}
