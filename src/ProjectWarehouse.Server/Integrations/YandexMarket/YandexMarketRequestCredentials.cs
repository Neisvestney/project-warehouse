using Neisvestney.Marketplaces.YandexMarket;
using ProjectWarehouse.Server.Integrations.Ozon;

namespace ProjectWarehouse.Server.Integrations.YandexMarket;

public sealed class YandexMarketRequestCredentials(MarketplaceRequestContext context) : IYandexMarketCredentialsProvider
{
    public YandexMarketCredentials GetCredentials() => new(context.Credentials?.ApiKey
        ?? throw new InvalidOperationException("Yandex Market credentials are not in scope."));
}
