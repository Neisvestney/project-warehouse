using System.ComponentModel.DataAnnotations;
using ProjectWarehouse.Server.Infrastructure;

namespace ProjectWarehouse.Server.Models.Warehouses;

public class NodeItemsGroupItem : IHasNullableIdentity
{
    public Guid? Id { get; init; }
    public Guid CatalogItemId { get; init; }

    [Range(1, int.MaxValue)]
    public int Count { get; init; }
}
