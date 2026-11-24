using Neisvestney.Marketplaces.YandexMarket;
using ProjectWarehouse.Server.Infrastructure;
using ProjectWarehouse.Server.Integrations.Abstractions;
using ProjectWarehouse.Server.Integrations.Ozon;
using ProjectWarehouse.Server.Integrations.YandexMarket;
using ProjectWarehouse.Server.Models;
using ProjectWarehouse.Server.Models.Integrations;

namespace ProjectWarehouse.Server.Services;

public sealed class YandexMarketConnectionService(IYandexMarketClient client, MarketplaceRequestContext context)
    : IYandexMarketConnectionService
{
    public async Task<(IReadOnlyList<YandexMarketCampaignDto> Campaigns, AppProblemDetails? Problem)> DiscoverAsync(
        string apiKey, CancellationToken ct)
    {
        using var scope = context.Use(new MarketplaceCredentials(null, apiKey));
        try { return (await client.GetCampaignsAsync(ct), null); }
        catch (YandexMarketApiException ex)
        {
            return ([], AppProblems.Root(422, ex.StatusCode is 401 or 403
                ? ErrorCode.MarketplaceCredentialsInvalid : ErrorCode.MarketplaceApiError,
                "Yandex Market campaign discovery failed."));
        }
    }

    public async Task<(YandexMarketCampaignDto? Campaign, AppProblemDetails? Problem)> ValidateAsync(string apiKey,
        long? campaignId, long? businessId, CancellationToken ct)
    {
        if (campaignId is null)
            return (null, AppProblems.UnprocessableEntity("campaignId", ErrorCode.Required, "Select an FBS or FBY campaign."));
        var (campaigns, problem) = await DiscoverAsync(apiKey, ct);
        if (problem is not null) return (null, problem);
        var campaign = campaigns.FirstOrDefault(c => c.CampaignId == campaignId
            && (businessId is null || c.BusinessId == businessId));
        return campaign is null
            ? (null, AppProblems.UnprocessableEntity("campaignId", ErrorCode.ValidationError,
                "The key cannot access the selected FBS or FBY campaign."))
            : (campaign, null);
    }
}
