namespace ProjectWarehouse.Server.Models.Orders;

public class OrderBoxDto
{
    public Guid Id { get; init; }
    public string? Label { get; init; }
    public IReadOnlyList<OrderBoxComponentDto> Components { get; init; } = [];
}
