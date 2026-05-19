namespace ProjectWarehouse.Server.Models.InboundOrders;

public class TryAutoAssignRequest
{
    public IReadOnlyList<Guid> DraftItemsGroupIds { get; init; } = [];
}
