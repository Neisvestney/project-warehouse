namespace ProjectWarehouse.Server.Infrastructure;

public class InsufficientInventoryException(
    Guid nodeId,
    Guid catalogItemId,
    int available,
    int requested,
    string catalogItemName = "",
    string[]? nodePath = null)
    : Exception($"Insufficient inventory for catalog item '{catalogItemId}' in node '{nodeId}': requested {requested}, available {available}."), IExpectedFailure
{
    public Guid NodeId { get; } = nodeId;
    public Guid CatalogItemId { get; } = catalogItemId;
    public int Available { get; } = available;
    public int Requested { get; } = requested;
    public string CatalogItemName { get; } = catalogItemName;
    public string[] NodePath { get; } = nodePath ?? [];

    public int Missing => Requested - Available;

    /// <summary>Structured context for <c>AppFieldError.Args</c> so the client can format a readable message.</summary>
    public IReadOnlyDictionary<string, object> ToArgs() => new Dictionary<string, object>
    {
        ["itemName"] = CatalogItemName,
        ["requested"] = Requested,
        ["available"] = Available,
        ["missing"] = Missing,
        ["path"] = string.Join(" / ", NodePath),
    };
}
