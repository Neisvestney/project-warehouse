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

    /// <summary>
    /// Postings that are packed on the marketplace side and awaiting handover — the only ones WMS
    /// imports. Yields pages for the same reason as <see cref="FetchCardsAsync"/>.
    /// </summary>
    IAsyncEnumerable<IReadOnlyList<ExternalPosting>> FetchActivePostingsAsync(
        MarketplaceCredentials credentials, CancellationToken ct);

    /// <summary>
    /// Postings the marketplace no longer knows are <b>omitted</b> from the result rather than thrown:
    /// one dead posting must not fail a whole run.
    /// </summary>
    Task<IReadOnlyList<ExternalPostingStatus>> FetchPostingStatusesAsync(
        MarketplaceCredentials credentials, IReadOnlyList<string> postingNumbers, CancellationToken ct);

    /// <summary>
    /// Takes a list because marketplaces print in batches. Batch sizing and the "retry one at a time"
    /// rule live in the label service — the provider just answers for whatever it was given.
    /// </summary>
    Task<ExternalLabelDocument> FetchLabelDocumentAsync(
        MarketplaceCredentials credentials, IReadOnlyList<string> postingNumbers, CancellationToken ct);
}
