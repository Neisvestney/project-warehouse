namespace ProjectWarehouse.Server.Models.Orders;

public class AssemblyLastPicksRequest
{
    public IReadOnlyList<AssemblyLastPicksItemRequest> Items { get; init; } = [];
}

public class AssemblyLastPicksItemRequest
{
    public Guid CatalogItemId { get; init; }
    public Guid WarehouseId { get; init; }
}
