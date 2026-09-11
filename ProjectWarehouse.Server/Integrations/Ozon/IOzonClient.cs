using ProjectWarehouse.Server.Integrations.Abstractions;

namespace ProjectWarehouse.Server.Integrations.Ozon;

/// <summary>
/// Domain-shaped wrapper over the generated client: owns pagination and response mapping so the
/// generated types never escape this folder.
/// </summary>
public interface IOzonClient
{
    Task<IReadOnlyList<ExternalWarehouse>> GetWarehousesAsync(CancellationToken ct);

    IAsyncEnumerable<IReadOnlyList<ExternalCard>> GetCardsAsync(CancellationToken ct);

    Task<ExternalSellerInfo> GetSellerInfoAsync(CancellationToken ct);

    /// <summary>Postings in <c>awaiting_deliver</c> — packed on Ozon's side, ready to be assembled here.</summary>
    IAsyncEnumerable<IReadOnlyList<ExternalPosting>> GetActivePostingsAsync(CancellationToken ct);

    /// <summary>
    /// Postings Ozon no longer knows are <b>absent</b> from the result rather than reported; every
    /// failure throws.
    /// </summary>
    Task<IReadOnlyList<ExternalPostingStatus>> GetPostingStatusesAsync(
        IReadOnlyList<string> postingNumbers, CancellationToken ct);

    Task<ExternalLabelDocument> GetPackageLabelAsync(IReadOnlyList<string> postingNumbers, CancellationToken ct);

    /// <summary>Cheapest call that proves the credentials work.</summary>
    Task PingAsync(CancellationToken ct);
}
