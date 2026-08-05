using ProjectWarehouse.Server.Domain;

namespace ProjectWarehouse.Server.Integrations.Abstractions;

public interface IMarketplaceProviderRegistry
{
    IMarketplaceProvider Get(MarketplaceType type);

    bool TryGet(MarketplaceType type, out IMarketplaceProvider provider);

    IReadOnlyList<IMarketplaceProvider> All { get; }
}
