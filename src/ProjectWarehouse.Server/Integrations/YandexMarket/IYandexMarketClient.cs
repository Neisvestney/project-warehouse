using ProjectWarehouse.Server.Integrations.Abstractions;
using ProjectWarehouse.Server.Models.Integrations;

namespace ProjectWarehouse.Server.Integrations.YandexMarket;

public interface IYandexMarketClient
{
    Task<IReadOnlyList<YandexMarketCampaignDto>> GetCampaignsAsync(CancellationToken ct);
    Task<ExternalSellerInfo> GetSellerInfoAsync(CancellationToken ct);
    Task<IReadOnlyList<ExternalWarehouse>> GetWarehousesAsync(CancellationToken ct);
    IAsyncEnumerable<IReadOnlyList<ExternalCard>> GetCardsAsync(CancellationToken ct);
    IAsyncEnumerable<IReadOnlyList<ExternalPosting>> GetActivePostingsAsync(CancellationToken ct);
    IAsyncEnumerable<IReadOnlyList<ExternalPosting>> GetPostingsAsync(ExternalPostingQuery query, CancellationToken ct);
    Task<IReadOnlyList<ExternalPostingStatus>> GetPostingStatusesAsync(IReadOnlyList<string> ids, CancellationToken ct);
    IAsyncEnumerable<IReadOnlyList<ExternalReturn>> GetReturnsAsync(ExternalReturnQuery query, CancellationToken ct);
    IAsyncEnumerable<IReadOnlyList<ExternalAccrual>> GetAccrualsAsync(DateOnly from, DateOnly to, CancellationToken ct);
    Task<DateTime?> GetEarliestPostingDateAsync(CancellationToken ct);
    Task<ExternalLabelDocument> GetLabelsAsync(IReadOnlyList<string> ids, CancellationToken ct);
}
