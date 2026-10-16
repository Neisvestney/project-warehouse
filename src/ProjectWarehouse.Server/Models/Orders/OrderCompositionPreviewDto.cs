namespace ProjectWarehouse.Server.Models.Orders;

/// <summary>Trimmed composition of a single order, for the hover preview in the order list.</summary>
public class OrderCompositionPreviewDto
{
    public int BoxCount { get; init; }

    /// <summary>Distinct box components across the order, before the preview limit is applied.</summary>
    public int PositionCount { get; init; }

    /// <summary>Summed quantity of those components — the number the order list shows in its own column.</summary>
    public int TotalQuantity { get; init; }

    public IReadOnlyList<OrderCompositionPreviewBoxDto> Boxes { get; init; } = [];

    /// <summary>Positions left out of <see cref="Boxes"/> by the preview limit.</summary>
    public int HiddenPositionCount { get; init; }
}
