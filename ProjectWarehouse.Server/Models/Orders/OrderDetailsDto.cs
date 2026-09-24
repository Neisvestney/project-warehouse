using ProjectWarehouse.Server.Domain;
using ProjectWarehouse.Server.Models.Files;

namespace ProjectWarehouse.Server.Models.Orders;

public class OrderDetailsDto
{
    public Guid Id { get; init; }
    public int Number { get; init; }
    public OrderType Type { get; init; }
    public OrderStatus Status { get; init; }

    /// <summary>Exists on the marketplace but never passed through WMS — not assembled, deducts no stock.</summary>
    public bool IsExternal { get; init; }
    public string? Notes { get; init; }
    public DateTime? PlannedShipmentAt { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? AssembledAt { get; init; }
    public DateTime? ShippedAt { get; init; }
    /// <summary>Null for an external order: it never entered a WMS warehouse.</summary>
    public Guid? WarehouseId { get; init; }
    public string? WarehouseName { get; init; }
    public Guid? CreatedById { get; init; }
    public string? CreatedByName { get; init; }
    public MarketplaceOrderDto? MarketplaceOrder { get; init; }
    public IReadOnlyList<OrderMarketplaceItemDto> MarketplaceItems { get; init; } = [];

    /// <summary>Every return reported for the posting, cancelled requests included, by return date.</summary>
    public IReadOnlyList<MarketplaceReturnDto> MarketplaceReturns { get; init; } = [];

    public IReadOnlyList<OrderBoxDto> Boxes { get; init; } = [];
    public IReadOnlyList<AssemblyTaskDto> AssemblyTasks { get; init; } = [];
    public IReadOnlyList<DataFileLinkDto> Attachments { get; init; } = [];
    public IReadOnlyList<OrderTagDto> Tags { get; init; } = [];
}
