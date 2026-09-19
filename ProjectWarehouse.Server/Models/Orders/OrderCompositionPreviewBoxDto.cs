namespace ProjectWarehouse.Server.Models.Orders;

public class OrderCompositionPreviewBoxDto
{
    public Guid Id { get; init; }
    public string? Label { get; init; }

    /// <summary>
    /// Components of this box that fit into the preview limit. Empty when the whole box fell outside it —
    /// the box still ships in the list so that the client can number unlabeled boxes by position.
    /// </summary>
    public IReadOnlyList<OrderCompositionPreviewComponentDto> Components { get; init; } = [];
}
