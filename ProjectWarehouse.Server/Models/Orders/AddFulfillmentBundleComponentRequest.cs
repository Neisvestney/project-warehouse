using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace ProjectWarehouse.Server.Models.Orders;

public class AddFulfillmentBundleComponentRequest
{
    [JsonRequired]
    public Guid CatalogItemId { get; init; }

    [JsonRequired]
    public Guid SourceNodeId { get; init; }

    /// <summary>Quantity for Standard-type components. Zero for Unit-type.</summary>
    [Range(0, int.MaxValue)]
    public int Quantity { get; init; }

    /// <summary>Set for Unit-type components.</summary>
    public Guid? UnitInventoryItemId { get; init; }
}
