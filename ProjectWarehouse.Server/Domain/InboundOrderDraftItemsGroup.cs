using ProjectWarehouse.Server.Infrastructure;

namespace ProjectWarehouse.Server.Domain;

public class InboundOrderDraftItemsGroup: IHasIdentity
{
    public Guid Id { get; set; }
    
    public string Name { get; set; } = null!;
    public string Article { get; set; } = null!;
    public string? Barcode { get; set; } // Barcode associated with specific Characteristic
    public string? RootBarcode { get; set; }
    public string Characteristic { get; set; } = null!;
    public int Count {get; set;}
    public int Order { get; set; }
    
    public Guid? CatalogItemId { get; set; }
    public CatalogItem? CatalogItem { get; set; }

    public Guid? CatalogItemWithCharacteristicId {get; set;}
    public CatalogItemWithCharacteristic? CatalogItemWithCharacteristic {get; set;}

    public bool CreateNew { get; set; }
    
    public Guid InboundOrderId {get; set;}
    public InboundOrder InboundOrder { get; set; } = null!;
}
