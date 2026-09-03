using ProjectWarehouse.Server.Domain;

namespace ProjectWarehouse.Server.Models.Statistics;

/// <summary>Shared filter for every stock statistics endpoint.</summary>
public class StockMovementFilterRequest
{
    /// <summary>Inclusive first day, in the resolved time zone. Defaults to 29 days before <see cref="To"/>.</summary>
    public DateOnly? From { get; init; }

    /// <summary>Inclusive last day, in the resolved time zone. Defaults to today.</summary>
    public DateOnly? To { get; init; }

    /// <summary>Narrows the rows and, when the warehouse has a zone of its own, decides where the day breaks.</summary>
    public Guid? WarehouseId { get; init; }
    public Guid? StoragePlaceId { get; init; }
    public Guid? NodeId { get; init; }
    public Guid? UserId { get; init; }

    /// <summary>Catalog items to keep. Empty means all — in the pivot that also means the columns are picked by volume.</summary>
    public Guid[]? CatalogItemIds { get; init; }

    /// <summary>
    /// Receipt tags to keep — a row matches when its receipt carries any of them. Empty means all.
    /// Movements made outside a receipt never match, so a non-empty value also drops them.
    /// </summary>
    public Guid[]? ReceiptTagIds { get; init; }

    /// <summary>Action constants to keep (<c>receipt.placement_added</c>, <c>transfer.standard</c>, …). Empty means all.</summary>
    public string[]? Actions { get; init; }

    public StockMovementDirection[]? Directions { get; init; }
}
