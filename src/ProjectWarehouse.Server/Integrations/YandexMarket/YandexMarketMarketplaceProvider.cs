using System.Runtime.CompilerServices;
using Neisvestney.Marketplaces.YandexMarket;
using ProjectWarehouse.Server.Domain;
using ProjectWarehouse.Server.Integrations.Abstractions;
using ProjectWarehouse.Server.Integrations.Ozon;

namespace ProjectWarehouse.Server.Integrations.YandexMarket;

public sealed class YandexMarketMarketplaceProvider(IYandexMarketClient client, MarketplaceRequestContext context,
    ILogger<YandexMarketMarketplaceProvider> logger) : IMarketplaceProvider
{
    public MarketplaceType Type => MarketplaceType.YandexMarket;
    public MarketplaceCapabilities Capabilities => MarketplaceCapabilities.Warehouses | MarketplaceCapabilities.Cards
        | MarketplaceCapabilities.SellerInfo | MarketplaceCapabilities.Orders | MarketplaceCapabilities.Labels
        | MarketplaceCapabilities.Returns | MarketplaceCapabilities.Accruals | MarketplaceCapabilities.Fbo;
    public bool RequiresClientId => false;

    public async Task<CredentialsValidationResult> ValidateAsync(MarketplaceCredentials credentials, CancellationToken ct)
    {
        try
        {
            var campaigns = await CallAsync(credentials, () => client.GetCampaignsAsync(ct));
            return credentials.CampaignId is null || campaigns.Any(c => c.CampaignId == credentials.CampaignId
                    && c.BusinessId == credentials.BusinessId && c.Model == credentials.YandexMarketModel)
                ? CredentialsValidationResult.Valid()
                : CredentialsValidationResult.Invalid("The key cannot access the selected campaign and model.");
        }
        catch (MarketplaceApiException ex) when (ex.IsCredentialsRejected)
        {
            return CredentialsValidationResult.Invalid(ex.Message, ex.Args);
        }
    }

    public Task<IReadOnlyList<ExternalWarehouse>> FetchWarehousesAsync(MarketplaceCredentials credentials, CancellationToken ct) =>
        CallAsync(credentials, () => client.GetWarehousesAsync(ct));
    public Task<ExternalSellerInfo> FetchSellerInfoAsync(MarketplaceCredentials credentials, CancellationToken ct) =>
        CallAsync(credentials, () => client.GetSellerInfoAsync(ct));
    public IAsyncEnumerable<IReadOnlyList<ExternalCard>> FetchCardsAsync(MarketplaceCredentials credentials, CancellationToken ct) =>
        PagesAsync(credentials, client.GetCardsAsync(ct), ct);
    public IAsyncEnumerable<IReadOnlyList<ExternalPosting>> FetchActivePostingsAsync(MarketplaceCredentials credentials, CancellationToken ct) =>
        PostingPagesAsync(credentials, client.GetActivePostingsAsync(ct), ct);
    public IAsyncEnumerable<IReadOnlyList<ExternalPosting>> FetchPostingsAsync(MarketplaceCredentials credentials,
        ExternalPostingQuery query, CancellationToken ct) => PostingPagesAsync(credentials, client.GetPostingsAsync(query, ct), ct);
    public Task<IReadOnlyList<ExternalPostingStatus>> FetchPostingStatusesAsync(MarketplaceCredentials credentials,
        IReadOnlyList<string> postingNumbers, ExternalPostingScheme scheme, CancellationToken ct) =>
        (scheme == ExternalPostingScheme.Fbo) == (credentials.YandexMarketModel == YandexMarketModel.Fby)
            ? CallAsync(credentials, () => client.GetPostingStatusesAsync(postingNumbers, ct))
            : Task.FromResult<IReadOnlyList<ExternalPostingStatus>>([]);
    public IAsyncEnumerable<IReadOnlyList<ExternalReturn>> FetchReturnsAsync(MarketplaceCredentials credentials,
        ExternalReturnQuery query, CancellationToken ct) => PagesAsync(credentials, client.GetReturnsAsync(query, ct), ct);
    public IAsyncEnumerable<IReadOnlyList<ExternalAccrual>> FetchAccrualsAsync(MarketplaceCredentials credentials,
        DateOnly from, DateOnly to, CancellationToken ct) => PagesAsync(credentials, client.GetAccrualsAsync(from, to, ct), ct);
    public Task<DateTime?> FetchEarliestPostingDateAsync(MarketplaceCredentials credentials, CancellationToken ct) =>
        CallAsync(credentials, () => client.GetEarliestPostingDateAsync(ct));
    public Task<ExternalLabelDocument> FetchLabelDocumentAsync(MarketplaceCredentials credentials,
        IReadOnlyList<string> postingNumbers, CancellationToken ct) => CallAsync(credentials, () => client.GetLabelsAsync(postingNumbers, ct));
    public IAsyncEnumerable<ExternalBuyoutWindow> FetchBuyoutsAsync(MarketplaceCredentials credentials, DateOnly from,
        DateOnly to, bool followsBuyoutCall, bool oldestFirst, CancellationToken ct) => throw new NotSupportedException();
    public Task<IReadOnlyList<ExternalSupply>> FetchSuppliesAsync(MarketplaceCredentials credentials, CancellationToken ct) =>
        throw new NotSupportedException();
    public IAsyncEnumerable<ExternalSupplyPostings> FetchSupplyPostingsAsync(MarketplaceCredentials credentials,
        IReadOnlyList<string> supplyExternalIds, CancellationToken ct) => throw new NotSupportedException();

    private async Task<T> CallAsync<T>(MarketplaceCredentials credentials, Func<Task<T>> call)
    {
        using var scope = context.Use(credentials);
        try { return await call(); }
        catch (YandexMarketApiException ex) { throw Wrap(ex); }
    }

    private async IAsyncEnumerable<IReadOnlyList<ExternalPosting>> PostingPagesAsync(MarketplaceCredentials credentials,
        IAsyncEnumerable<IReadOnlyList<ExternalPosting>> source, [EnumeratorCancellation] CancellationToken ct)
    {
        var warehousesLoaded = false;
        string? campaignWarehouseId = null;
        await foreach (var page in PagesAsync(credentials, source, ct))
        {
            if (page.Any(p => p.Scheme == ExternalPostingScheme.Fbs && string.IsNullOrWhiteSpace(p.WarehouseExternalId)))
            {
                if (!warehousesLoaded)
                {
                    var ids = (await FetchWarehousesAsync(credentials, ct)).Select(w => w.ExternalId)
                        .Distinct(StringComparer.Ordinal).Take(2).ToArray();
                    campaignWarehouseId = ids.Length == 1 ? ids[0] : null;
                    warehousesLoaded = true;
                }
                if (campaignWarehouseId is not null)
                {
                    yield return page.Select(p => p.Scheme == ExternalPostingScheme.Fbs
                            && string.IsNullOrWhiteSpace(p.WarehouseExternalId)
                        ? p with {WarehouseExternalId = campaignWarehouseId} : p).ToArray();
                    continue;
                }
            }
            yield return page;
        }
    }

    private async IAsyncEnumerable<T> PagesAsync<T>(MarketplaceCredentials credentials, IAsyncEnumerable<T> source,
        [EnumeratorCancellation] CancellationToken ct)
    {
        await using var iterator = source.GetAsyncEnumerator(ct);
        while (true)
        {
            bool next;
            using (context.Use(credentials))
            {
                try { next = await iterator.MoveNextAsync(); }
                catch (YandexMarketApiException ex) { throw Wrap(ex); }
            }
            if (!next) yield break;
            yield return iterator.Current;
        }
    }

    private MarketplaceApiException Wrap(YandexMarketApiException ex)
    {
        var response = ex.Response is { Length: > 4096 } body ? body[..4096] : ex.Response;
        logger.Log(ex.StatusCode == 420 ? LogLevel.Information : LogLevel.Warning,
            "Yandex Market request failed with HTTP {Status}: {Response}", ex.StatusCode, response);
        return new("Yandex Market request failed.", ex.StatusCode, response, ex);
    }
}
