namespace ProjectWarehouse.Server.Models.Orders;

public class AssemblyCheckPreviewRequest
{
    public IReadOnlyList<Guid> OrderIds { get; init; } = [];
}
