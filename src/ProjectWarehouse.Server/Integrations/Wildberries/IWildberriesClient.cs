using ProjectWarehouse.Server.Integrations.Abstractions;

namespace ProjectWarehouse.Server.Integrations.Wildberries;

/// <summary>
/// Wildberries in the provider's vocabulary. Every call expects the account's credentials in
/// <see cref="Ozon.MarketplaceRequestContext"/>; failures surface as the generated client's exception.
/// </summary>
public interface IWildberriesClient
{
    Task PingAsync(CancellationToken ct);

    Task<ExternalSellerInfo> GetSellerInfoAsync(CancellationToken ct);

    Task<IReadOnlyList<ExternalWarehouse>> GetWarehousesAsync(CancellationToken ct);

    /// <summary>One card per size: the size's <c>chrtID</c> is both the external id and the SKU.</summary>
    IAsyncEnumerable<IReadOnlyList<ExternalCard>> GetCardsAsync(CancellationToken ct);

    IAsyncEnumerable<IReadOnlyList<ExternalPosting>> GetActivePostingsAsync(CancellationToken ct);

    IAsyncEnumerable<IReadOnlyList<ExternalPosting>> GetPostingsAsync(ExternalPostingQuery query, CancellationToken ct);

    Task<IReadOnlyList<ExternalPostingStatus>> GetPostingStatusesAsync(IReadOnlyList<string> postingNumbers,
        CancellationToken ct);

    IAsyncEnumerable<IReadOnlyList<ExternalReturn>> GetReturnsAsync(ExternalReturnQuery query, CancellationToken ct);

    IAsyncEnumerable<IReadOnlyList<ExternalAccrual>> GetAccrualsAsync(DateOnly from, DateOnly to, CancellationToken ct);

    Task<DateTime?> GetEarliestPostingDateAsync(CancellationToken ct);

    Task<IReadOnlyList<ExternalSupply>> GetSuppliesAsync(CancellationToken ct);

    /// <summary>A supply WB answers 404 for is skipped: it was deleted after the listing was read.</summary>
    IAsyncEnumerable<ExternalSupplyPostings> GetSupplyPostingsAsync(IReadOnlyList<string> supplyIds,
        CancellationToken ct);

    Task<ExternalLabelDocument> GetStickersAsync(IReadOnlyList<string> postingNumbers, CancellationToken ct);
}
