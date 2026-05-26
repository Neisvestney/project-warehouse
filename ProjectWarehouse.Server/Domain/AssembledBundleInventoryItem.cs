namespace ProjectWarehouse.Server.Domain;

public class AssembledBundleInventoryItem : InventoryItem
{
    public ICollection<AssembledBundleInventoryItemComponent> Components { get; set; } = [];
}
