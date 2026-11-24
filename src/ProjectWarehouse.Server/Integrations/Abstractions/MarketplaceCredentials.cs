using ProjectWarehouse.Server.Domain;

namespace ProjectWarehouse.Server.Integrations.Abstractions;

// ClientId is null for providers that authenticate with a token alone (Wildberries).
public record MarketplaceCredentials(string? ClientId, string ApiKey, long? BusinessId = null, long? CampaignId = null,
    YandexMarketModel? YandexMarketModel = null)
{
    public override string ToString() => "MarketplaceCredentials { [redacted] }";
}
