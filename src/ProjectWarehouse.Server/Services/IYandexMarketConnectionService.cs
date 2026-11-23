using ProjectWarehouse.Server.Models;
using ProjectWarehouse.Server.Models.Integrations;

namespace ProjectWarehouse.Server.Services;

public interface IYandexMarketConnectionService
{
    Task<(IReadOnlyList<YandexMarketCampaignDto> Campaigns, AppProblemDetails? Problem)> DiscoverAsync(string apiKey,
        CancellationToken ct);
    Task<(YandexMarketCampaignDto? Campaign, AppProblemDetails? Problem)> ValidateAsync(string apiKey,
        long? campaignId, long? businessId, CancellationToken ct);
}
