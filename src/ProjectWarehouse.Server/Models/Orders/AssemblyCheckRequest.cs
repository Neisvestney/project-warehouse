using System.Text.Json.Serialization;

namespace ProjectWarehouse.Server.Models.Orders;

public class AssemblyCheckRequest
{
    public IReadOnlyList<AssemblyCheckItemRequest> Items { get; init; } = [];
}

public class AssemblyCheckItemRequest
{
    /// <summary>The component's own catalog item; a Variation defers to <c>fulfillment.resolvedCatalogItemId</c>.</summary>
    public Guid CatalogItemId { get; init; }

    [JsonRequired] public AddFulfillmentRequest Fulfillment { get; init; } = null!;
}
