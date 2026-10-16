using ProjectWarehouse.Server.Domain;

namespace ProjectWarehouse.Server.Integrations.Abstractions;

public class MarketplaceProviderRegistry : IMarketplaceProviderRegistry
{
    private readonly Dictionary<MarketplaceType, IMarketplaceProvider> _byType;

    public MarketplaceProviderRegistry(IEnumerable<IMarketplaceProvider> providers)
    {
        _byType = providers.ToDictionary(p => p.Type);
        All = [.. _byType.Values];
    }

    public IReadOnlyList<IMarketplaceProvider> All { get; }

    public IMarketplaceProvider Get(MarketplaceType type) =>
        _byType.TryGetValue(type, out var provider)
            ? provider
            : throw new NotSupportedException($"No marketplace provider registered for {type}.");

    public bool TryGet(MarketplaceType type, out IMarketplaceProvider provider) =>
        _byType.TryGetValue(type, out provider!);
}
