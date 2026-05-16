using ProjectWarehouse.Server.Infrastructure;

namespace ProjectWarehouse.Server.Domain;

public abstract class ItemsGroup : IHasIdentity
{
    public Guid Id {get; set;}
    public Guid CatalogItemWithCharacteristicId { get; set; }
    public CatalogItemWithCharacteristic CatalogItemWithCharacteristic { get; set; } = null!;
    public int Count {get; set;}
}