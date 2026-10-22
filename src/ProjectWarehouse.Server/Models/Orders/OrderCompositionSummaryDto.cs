namespace ProjectWarehouse.Server.Models.Orders;

/// <summary>Box components of several orders summed per catalog item.</summary>
public class OrderCompositionSummaryDto
{
    public IReadOnlyList<OrderCompositionSummaryItemDto> Items { get; init; } = [];
}

public class OrderCompositionSummaryItemDto
{
    public Guid CatalogItemId { get; init; }
    public string CatalogItemName { get; init; } = null!;
    public string Article { get; init; } = null!;
    public int Quantity { get; init; }

    /// <summary>How many of the requested orders contain the item.</summary>
    public int OrderCount { get; init; }
}
