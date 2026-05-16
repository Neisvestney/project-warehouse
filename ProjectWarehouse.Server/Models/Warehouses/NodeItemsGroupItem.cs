using System.ComponentModel.DataAnnotations;

namespace ProjectWarehouse.Server.Models.Warehouses;

public class NodeItemsGroupItem
{
    public Guid? Id { get; init; }
    public Guid CatalogItemWithCharacteristicId { get; init; }

    [Range(1, int.MaxValue)]
    public int Count { get; init; }
}
