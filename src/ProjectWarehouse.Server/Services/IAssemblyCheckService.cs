using ProjectWarehouse.Server.Domain;
using ProjectWarehouse.Server.Models.Orders;

namespace ProjectWarehouse.Server.Services;

/// <summary>Stock arithmetic of assembly — what a fulfillment takes off the shelves and whether the shelves hold it.</summary>
public interface IAssemblyCheckService
{
    /// <summary>
    /// The stock a single fulfillment takes off the shelves, per node and catalog item. Unit picks are left
    /// out: they move one named item rather than draw on a count, and never report a shortage.
    /// </summary>
    IEnumerable<(Guid NodeId, Guid CatalogItemId, int Quantity)> EnumerateStockDemand(
        Guid componentCatalogItemId, CatalogItemType componentType, AddFulfillmentRequest fulfillment);

    /// <summary>Current count of each node/catalog-item pair, missing pairs meaning an empty shelf.</summary>
    Task<Dictionary<(Guid NodeId, Guid CatalogItemId), int>> ReadStockAsync(
        IReadOnlyCollection<(Guid NodeId, Guid CatalogItemId)> pairs, CancellationToken ct = default);

    /// <summary>
    /// What is left to assemble in the given orders, as assembly tasks. Only Draft, Confirmed and Assembly
    /// orders bound to a warehouse take part; the rest are skipped. <paramref name="accessible"/> is the
    /// caller's access-filtered order query.
    /// </summary>
    Task<AssemblyCheckPreviewDto> PreviewAsync(
        IQueryable<Order> accessible, IReadOnlyCollection<Guid> orderIds, CancellationToken ct = default);

    /// <summary>Sums the stock demand of every item and reports each node/item pair the shelves cannot cover. Writes nothing.</summary>
    Task<AssemblyCheckResponse> CheckAsync(
        IReadOnlyList<AssemblyCheckItemRequest> items, CancellationToken ct = default);
}
