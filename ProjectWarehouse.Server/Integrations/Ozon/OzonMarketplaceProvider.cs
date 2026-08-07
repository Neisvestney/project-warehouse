using System.Runtime.CompilerServices;
using ProjectWarehouse.Server.Domain;
using ProjectWarehouse.Server.Integrations.Abstractions;
using ProjectWarehouse.Server.Integrations.Ozon.Generated;

namespace ProjectWarehouse.Server.Integrations.Ozon;

public class OzonMarketplaceProvider(
    IOzonClient client,
    MarketplaceRequestContext requestContext,
    ILogger<OzonMarketplaceProvider> logger) : IMarketplaceProvider
{
    public MarketplaceType Type => MarketplaceType.Ozon;

    public MarketplaceCapabilities Capabilities =>
        MarketplaceCapabilities.Warehouses | MarketplaceCapabilities.Cards | MarketplaceCapabilities.SellerInfo
        | MarketplaceCapabilities.Orders | MarketplaceCapabilities.Labels;

    public bool RequiresClientId => true;

    public async Task<CredentialsValidationResult> ValidateAsync(MarketplaceCredentials credentials, CancellationToken ct)
    {
        using var _ = requestContext.Use(credentials);
        try
        {
            await client.PingAsync(ct);
            return CredentialsValidationResult.Valid();
        }
        catch (OzonApiException ex) when (ex.StatusCode is 401 or 403)
        {
            logger.LogWarning("Ozon rejected the credentials with {Status}", ex.StatusCode);
            return CredentialsValidationResult.Invalid(Describe(ex), Wrap(ex).Args);
        }
        catch (OzonApiException ex)
        {
            throw Wrap(ex);
        }
    }

    public async Task<IReadOnlyList<ExternalWarehouse>> FetchWarehousesAsync(MarketplaceCredentials credentials, CancellationToken ct)
    {
        using var _ = requestContext.Use(credentials);
        try
        {
            return await client.GetWarehousesAsync(ct);
        }
        catch (OzonApiException ex)
        {
            throw Wrap(ex);
        }
    }

    public async Task<ExternalSellerInfo> FetchSellerInfoAsync(MarketplaceCredentials credentials, CancellationToken ct)
    {
        using var _ = requestContext.Use(credentials);
        try
        {
            return await client.GetSellerInfoAsync(ct);
        }
        catch (OzonApiException ex)
        {
            throw Wrap(ex);
        }
    }

    public async IAsyncEnumerable<IReadOnlyList<ExternalCard>> FetchCardsAsync(
        MarketplaceCredentials credentials, [EnumeratorCancellation] CancellationToken ct)
    {
        var pages = client.GetCardsAsync(ct).GetAsyncEnumerator(ct);
        try
        {
            while (true)
            {
                // try/catch cannot wrap a yield, so the move is stepped manually
                bool hasNext;
                try
                {
                    // The scope is re-entered per page on purpose. An AsyncLocal written inside an async
                    // iterator dies at the yield: control returns to the consumer, its ExecutionContext is
                    // restored, and the next MoveNextAsync resumes the body without ever re-running the
                    // assignment. Opening the scope around each move keeps the credentials in the context
                    // that the HTTP handler actually observes.
                    using var _ = requestContext.Use(credentials);
                    hasNext = await pages.MoveNextAsync();
                }
                catch (OzonApiException ex)
                {
                    throw Wrap(ex);
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

    public async IAsyncEnumerable<IReadOnlyList<ExternalPosting>> FetchActivePostingsAsync(
        MarketplaceCredentials credentials, [EnumeratorCancellation] CancellationToken ct)
    {
        var pages = client.GetActivePostingsAsync(ct).GetAsyncEnumerator(ct);
        try
        {
            while (true)
            {
                bool hasNext;
                try
                {
                    // Same reason as FetchCardsAsync: an AsyncLocal written inside an async iterator does
                    // not survive the yield, so the scope is opened around every move.
                    using var _ = requestContext.Use(credentials);
                    hasNext = await pages.MoveNextAsync();
                }
                catch (OzonApiException ex)
                {
                    throw Wrap(ex);
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

    public async Task<IReadOnlyList<ExternalPostingStatus>> FetchPostingStatusesAsync(
        MarketplaceCredentials credentials, IReadOnlyList<string> postingNumbers, CancellationToken ct)
    {
        // a plain async method, so one scope covers the whole loop
        using var _ = requestContext.Use(credentials);

        var statuses = new List<ExternalPostingStatus>(postingNumbers.Count);
        try
        {
            foreach (var postingNumber in postingNumbers)
            {
                // postings Ozon has forgotten come back null and are simply left out
                if (await client.GetPostingStatusAsync(postingNumber, ct) is { } status)
                    statuses.Add(status);
            }
        }
        catch (OzonApiException ex)
        {
            throw Wrap(ex);
        }

        return statuses;
    }

    public async Task<ExternalLabelDocument> FetchLabelDocumentAsync(
        MarketplaceCredentials credentials, IReadOnlyList<string> postingNumbers, CancellationToken ct)
    {
        using var _ = requestContext.Use(credentials);
        try
        {
            return await client.GetPackageLabelAsync(postingNumbers, ct);
        }
        catch (OzonApiException ex) when (OzonLabelHeuristics.LooksNotReady(ex.Response))
        {
            // Ozon also reports "not printed yet" as a rejection, not only as a 200 with a JSON body
            logger.LogInformation(
                "Ozon rejected the label request for {Count} posting(s) as not ready ({Status})",
                postingNumbers.Count, ex.StatusCode);
            return new ExternalLabelDocument(false, postingNumbers, null, null);
        }
        catch (OzonApiException ex)
        {
            throw Wrap(ex);
        }
    }

    private static MarketplaceApiException Wrap(OzonApiException ex) =>
        new(Describe(ex), ex.StatusCode, Body(ex), ex);

    /// <summary>
    /// A 2xx here means the call succeeded and <i>we</i> failed to read it, so the generated client's own
    /// message plus the JSON path from the inner exception is the whole diagnosis — "Ozon responded with
    /// 200" would send the reader hunting on the wrong side of the wire.
    /// </summary>
    private static string Describe(OzonApiException ex) =>
        ex.StatusCode is >= 200 and < 300
            ? $"{ex.Message} {ex.InnerException?.Message}".Trim()
            : $"Ozon responded with {ex.StatusCode}.";

    /// <summary>Response body only, truncated. The Api-Key travels in request headers and must never surface.</summary>
    private static string? Body(OzonApiException ex) =>
        string.IsNullOrWhiteSpace(ex.Response) ? null : ex.Response[..Math.Min(ex.Response.Length, BodyLimit)];

    // long enough for an Ozon rejection to stay readable in the sync run, short enough not to store a page of cards
    private const int BodyLimit = 2000;
}
