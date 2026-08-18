using ProjectWarehouse.Server.Models.Orders;

namespace ProjectWarehouse.Server.Services;

/// <summary>
/// Either the whole print job or nothing. A batch of 30 quietly arriving with 28 labels means two
/// unshipped boxes, so an unready posting fails the request instead of trimming the file.
/// </summary>
/// <param name="Pdf">Merged document in the requested order; null when something blocked the job.</param>
/// <param name="NotReadyPostingNumbers">Postings the marketplace has not printed yet.</param>
/// <param name="NonMarketplaceOrderIds">Requested orders that are not marketplace orders at all.</param>
/// <param name="NotAwaitingDeliverPostingNumbers">Postings the marketplace no longer prints labels for.</param>
public record LabelBundle(
    byte[]? Pdf,
    IReadOnlyList<string> NotReadyPostingNumbers,
    IReadOnlyList<Guid> NonMarketplaceOrderIds,
    IReadOnlyList<string> NotAwaitingDeliverPostingNumbers);

public interface IMarketplaceLabelService
{
    Task<LabelBundle> BuildAsync(IReadOnlyList<Guid> orderIds, OrderLabelsGrouping grouping, Guid? userId,
        CancellationToken ct);
}
