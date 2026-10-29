using System.Runtime.CompilerServices;
using Neisvestney.Marketplaces.Wildberries;
using ProjectWarehouse.Server.Domain;
using ProjectWarehouse.Server.Integrations.Abstractions;
using ProjectWarehouse.Server.Integrations.Ozon;

namespace ProjectWarehouse.Server.Integrations.Wildberries;

public class WildberriesMarketplaceProvider(
    IWildberriesClient client,
    MarketplaceRequestContext requestContext,
    ILogger<WildberriesMarketplaceProvider> logger) : IMarketplaceProvider
{
    public MarketplaceType Type => MarketplaceType.Wildberries;

    public MarketplaceCapabilities Capabilities =>
        MarketplaceCapabilities.Warehouses | MarketplaceCapabilities.Cards | MarketplaceCapabilities.SellerInfo
        | MarketplaceCapabilities.Orders | MarketplaceCapabilities.Labels | MarketplaceCapabilities.Returns
        | MarketplaceCapabilities.Accruals | MarketplaceCapabilities.Supplies
        | MarketplaceCapabilities.ReversedSaleReturns;

    public bool RequiresClientId => false;

    public async Task<CredentialsValidationResult> ValidateAsync(MarketplaceCredentials credentials, CancellationToken ct)
    {
        using var _ = requestContext.Use(credentials);
        try
        {
            await client.PingAsync(ct);
            return CredentialsValidationResult.Valid();
        }
        catch (WildberriesApiException ex) when (ex.StatusCode is 401 or 403)
        {
            logger.LogWarning("Wildberries rejected the token with {Status}", ex.StatusCode);
            logger.LogFailedResponse(ex, LogLevel.Warning);
            return CredentialsValidationResult.Invalid(Describe(ex), Wrap(ex).Args);
        }
        catch (WildberriesApiException ex)
        {
            throw LogAndWrap(ex);
        }
    }

    public Task<IReadOnlyList<ExternalWarehouse>> FetchWarehousesAsync(MarketplaceCredentials credentials,
        CancellationToken ct) =>
        CallAsync(credentials, () => client.GetWarehousesAsync(ct));

    public Task<ExternalSellerInfo> FetchSellerInfoAsync(MarketplaceCredentials credentials, CancellationToken ct) =>
        CallAsync(credentials, () => client.GetSellerInfoAsync(ct));

    public IAsyncEnumerable<IReadOnlyList<ExternalCard>> FetchCardsAsync(MarketplaceCredentials credentials,
        CancellationToken ct) =>
        PagesAsync(credentials, client.GetCardsAsync(ct), ct);

    public IAsyncEnumerable<IReadOnlyList<ExternalPosting>> FetchActivePostingsAsync(
        MarketplaceCredentials credentials, CancellationToken ct) =>
        PagesAsync(credentials, client.GetActivePostingsAsync(ct), ct);

    public IAsyncEnumerable<IReadOnlyList<ExternalPosting>> FetchPostingsAsync(MarketplaceCredentials credentials,
        ExternalPostingQuery query, CancellationToken ct) =>
        PagesAsync(credentials, client.GetPostingsAsync(query, ct), ct);

    public Task<IReadOnlyList<ExternalPostingStatus>> FetchPostingStatusesAsync(MarketplaceCredentials credentials,
        IReadOnlyList<string> postingNumbers, ExternalPostingScheme scheme, CancellationToken ct) =>
        scheme == ExternalPostingScheme.Fbs
            ? CallAsync(credentials, () => client.GetPostingStatusesAsync(postingNumbers, ct))
            : Task.FromResult<IReadOnlyList<ExternalPostingStatus>>([]);

    public IAsyncEnumerable<IReadOnlyList<ExternalReturn>> FetchReturnsAsync(MarketplaceCredentials credentials,
        ExternalReturnQuery query, CancellationToken ct) =>
        PagesAsync(credentials, client.GetReturnsAsync(query, ct), ct);

    public IAsyncEnumerable<IReadOnlyList<ExternalAccrual>> FetchAccrualsAsync(MarketplaceCredentials credentials,
        DateOnly from, DateOnly to, CancellationToken ct) =>
        PagesAsync(credentials, client.GetAccrualsAsync(from, to, ct), ct, LogLevel.Information);

    public IAsyncEnumerable<ExternalBuyoutWindow> FetchBuyoutsAsync(MarketplaceCredentials credentials, DateOnly from,
        DateOnly to, bool followsBuyoutCall, bool oldestFirst, CancellationToken ct) =>
        throw new NotSupportedException("Wildberries has no buyout report.");

    public Task<IReadOnlyList<ExternalSupply>> FetchSuppliesAsync(MarketplaceCredentials credentials,
        CancellationToken ct) =>
        CallAsync(credentials, () => client.GetSuppliesAsync(ct));

    public IAsyncEnumerable<ExternalSupplyPostings> FetchSupplyPostingsAsync(MarketplaceCredentials credentials,
        IReadOnlyList<string> supplyExternalIds, CancellationToken ct) =>
        PagesAsync(credentials, client.GetSupplyPostingsAsync(supplyExternalIds, ct), ct);

    public Task<DateTime?> FetchEarliestPostingDateAsync(MarketplaceCredentials credentials, CancellationToken ct) =>
        CallAsync(credentials, () => client.GetEarliestPostingDateAsync(ct));

    // the label service retries a rejected batch one posting at a time, so Error would turn a retry into a log storm
    public Task<ExternalLabelDocument> FetchLabelDocumentAsync(MarketplaceCredentials credentials,
        IReadOnlyList<string> postingNumbers, CancellationToken ct) =>
        CallAsync(credentials, () => client.GetStickersAsync(postingNumbers, ct), LogLevel.Warning);

    private async Task<T> CallAsync<T>(MarketplaceCredentials credentials, Func<Task<T>> call,
        LogLevel level = LogLevel.Error)
    {
        using var _ = requestContext.Use(credentials);
        try
        {
            return await call();
        }
        catch (WildberriesApiException ex)
        {
            throw LogAndWrap(ex, level);
        }
    }

    // rateLimitLevel: the level a 429 is written at; the report quota runs out routinely
    private async IAsyncEnumerable<T> PagesAsync<T>(MarketplaceCredentials credentials, IAsyncEnumerable<T> source,
        [EnumeratorCancellation] CancellationToken ct, LogLevel rateLimitLevel = LogLevel.Error)
    {
        var pages = source.GetAsyncEnumerator(ct);
        try
        {
            while (true)
            {
                bool hasNext;
                try
                {
                    // An AsyncLocal written inside an async iterator does not survive the yield, so the scope is
                    // opened around every move; see OzonMarketplaceProvider.FetchCardsAsync.
                    using var _ = requestContext.Use(credentials);
                    hasNext = await pages.MoveNextAsync();
                }
                catch (WildberriesApiException ex) when (ex.StatusCode == StatusCodes.Status429TooManyRequests)
                {
                    throw LogAndWrap(ex, rateLimitLevel);
                }
                catch (WildberriesApiException ex)
                {
                    throw LogAndWrap(ex);
                }

                if (!hasNext)
                    yield break;

                yield return pages.Current;
            }
        }
        finally
        {
            await pages.DisposeAsync();
        }
    }

    private MarketplaceApiException LogAndWrap(WildberriesApiException ex, LogLevel level = LogLevel.Error)
    {
        logger.LogFailedResponse(ex, level);
        return Wrap(ex);
    }

    private static MarketplaceApiException Wrap(WildberriesApiException ex) =>
        new(Describe(ex), ex.StatusCode, Body(ex), ex);

    // a 2xx means the call succeeded and the answer did not read, see OzonMarketplaceProvider.Describe
    private static string Describe(WildberriesApiException ex) =>
        ex.StatusCode is >= 200 and < 300
            ? $"{ex.Message} {ex.InnerException?.Message}".Trim()
            : $"Wildberries responded with {ex.StatusCode}.";

    /// <summary>Response body only, truncated. The token travels in request headers and must never surface.</summary>
    private static string? Body(WildberriesApiException ex) =>
        string.IsNullOrWhiteSpace(ex.Response) ? null : ex.Response[..Math.Min(ex.Response.Length, BodyLimit)];

    private const int BodyLimit = 2000;
}
