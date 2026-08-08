using System.ComponentModel.DataAnnotations;
using ProjectWarehouse.Server.Domain;

namespace ProjectWarehouse.Server.Models.Statistics;

/// <summary>Shared filter for every stock statistics endpoint.</summary>
public class StockMovementFilterRequest
{
    /// <summary>Inclusive first day, in the caller's time zone. Defaults to 29 days before <see cref="To"/>.</summary>
    public DateOnly? From { get; init; }

    /// <summary>Inclusive last day, in the caller's time zone. Defaults to today.</summary>
    public DateOnly? To { get; init; }

    /// <summary>
    /// Offset of the caller's time zone from UTC. A day boundary is meaningless without it — a warehouse
    /// in UTC+3 would see an evening shift's work land on the next day.
    /// </summary>
    [Range(-840, 840)]
    public int UtcOffsetMinutes { get; init; }

    public Guid? WarehouseId { get; init; }
    public Guid? StoragePlaceId { get; init; }
    public Guid? NodeId { get; init; }
    public Guid? UserId { get; init; }

    /// <summary>Catalog items to keep. Empty means all — in the pivot that also means the columns are picked by volume.</summary>
    public Guid[]? CatalogItemIds { get; init; }

    /// <summary>Action constants to keep (<c>receipt.placement_added</c>, <c>transfer.standard</c>, …). Empty means all.</summary>
    public string[]? Actions { get; init; }

    public StockMovementDirection[]? Directions { get; init; }
}
