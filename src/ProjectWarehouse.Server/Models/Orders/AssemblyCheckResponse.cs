namespace ProjectWarehouse.Server.Models.Orders;

public class AssemblyCheckResponse
{
    /// <summary>One <c>insufficientInventory</c> error per catalog item and storage node the request asks too much of.</summary>
    public IReadOnlyList<AppFieldError> InsufficientInventoryErrors { get; init; } = [];
}
