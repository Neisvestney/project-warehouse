using ProjectWarehouse.Server.Domain;

namespace ProjectWarehouse.Server.Integrations.Abstractions;

/// <summary>
/// Everything marketplace-specific lives behind this interface — the sync service knows nothing about
/// Ozon, its pagination styles, or its generated client.
/// </summary>
public interface IMarketplaceProvider
{
    MarketplaceType Type { get; }

    MarketplaceCapabilities Capabilities { get; }

    /// <summary>True when the provider needs a client id alongside the API key.</summary>
    bool RequiresClientId { get; }

    Task<CredentialsValidationResult> ValidateAsync(MarketplaceCredentials credentials, CancellationToken ct);

    Task<IReadOnlyList<ExternalWarehouse>> FetchWarehousesAsync(MarketplaceCredentials credentials, CancellationToken ct);

    /// <summary>Yields pages, not one list: a seller may have tens of thousands of cards.</summary>
    IAsyncEnumerable<IReadOnlyList<ExternalCard>> FetchCardsAsync(MarketplaceCredentials credentials, CancellationToken ct);

    /// <summary>Only called when the provider declares <see cref="MarketplaceCapabilities.SellerInfo"/>.</summary>
    Task<ExternalSellerInfo> FetchSellerInfoAsync(MarketplaceCredentials credentials, CancellationToken ct);
}
