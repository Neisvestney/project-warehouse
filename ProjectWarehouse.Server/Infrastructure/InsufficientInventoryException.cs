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
    public IReadOnlyDictionary<string, object> ToArgs() =>
        MakeArgs(CatalogItemName, string.Join(" / ", NodePath), Requested, Available);

    /// <summary>
    /// The same arg shape for a shortage assembled from figures of their own — a batch rollup counts demand
    /// across several positions and reads the stock itself, so it has no single exception to render.
    /// </summary>
    public static IReadOnlyDictionary<string, object> MakeArgs(
        string itemName, string path, int requested, int available) => new Dictionary<string, object>
    {
        ["itemName"] = itemName,
        ["requested"] = requested,
        ["available"] = available,
        ["missing"] = Math.Max(0, requested - available),
        ["path"] = path,
    };
}
