using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace ProjectWarehouse.Server.Models.Receipts;

public class CreateAssembledBundlePlacementRequest
{
    [JsonRequired]
    public Guid StoragePlaceNodeId { get; init; }

    [Required]
    [MinLength(1)]
    public IReadOnlyList<AssembledBundlePlacementComponentRequest> Components { get; init; } = [];
}

public class AssembledBundlePlacementComponentRequest
{
    /// <summary>The catalog item ID of this component.</summary>
    [JsonRequired]
    public Guid CatalogItemId { get; init; }

    /// <summary>
    /// Quantity to place. Required for Standard/Bundle components.
    /// Null when a unit item is supplied via <see cref="UnitInventoryItemId"/> or <see cref="NewUnitItem"/>.
    /// </summary>
    [Range(1, int.MaxValue)]
    public int? Quantity { get; init; }

    /// <summary>Existing <c>UnitInventoryItem</c> to include as a component. Mutually exclusive with <see cref="NewUnitItem"/>.</summary>
    public Guid? UnitInventoryItemId { get; init; }

    /// <summary>
    /// Creates a new unit inventory item and includes it as a component.
    /// Mutually exclusive with <see cref="UnitInventoryItemId"/>.
    /// </summary>
    public CreateUnitItemRequest? NewUnitItem { get; init; }
}
