using ProjectWarehouse.Server.Domain;

namespace ProjectWarehouse.Server.Models.Orders;

public class AssemblyTaskBoxComponentDto
{
    public Guid Id { get; init; }
    public Guid CatalogItemId { get; init; }
    public string CatalogItemName { get; init; } = null!;
    public CatalogItemType CatalogItemType { get; init; }
    public int Quantity { get; init; }
    public IReadOnlyList<AssemblyFulfillmentDto> Fulfillments { get; init; } = [];

    /// <summary>
    /// True if this component's own type is Unit, or if it's a Bundle/Variation whose
    /// composition resolves to a Unit item anywhere in its nested tree. Used to exclude such
    /// components from bulk/batch assembly eligibility, since a Unit fulfillment must reference
    /// a distinct physical inventory instance per task and can't be shared/copied across tasks.
    /// </summary>
    public bool ContainsUnit { get; set; }
}
