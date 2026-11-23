using System.Globalization;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Options;
using Neisvestney.Marketplaces.YandexMarket;
using ProjectWarehouse.Server.Domain;
using ProjectWarehouse.Server.Infrastructure.Labels;
using ProjectWarehouse.Server.Infrastructure.Marketplaces;
using ProjectWarehouse.Server.Integrations.Abstractions;
using ProjectWarehouse.Server.Integrations.Ozon;
using ProjectWarehouse.Server.Models.Integrations;

namespace ProjectWarehouse.Server.Integrations.YandexMarket;

public sealed partial class YandexMarketClient(
    IYandexMarketApiClient api,
    MarketplaceRequestContext context,
    IHttpClientFactory httpClients,
    YandexMarketReportQuota quota,
    IOptions<MarketplacesOptions> options,
    ILogger<YandexMarketClient> logger) : IYandexMarketClient
{
    private readonly YandexMarketOptions _options = options.Value.YandexMarket;
    private readonly LabelsOptions _labelOptions = options.Value.Labels;
    private MarketplaceCredentials Credentials => context.Credentials
        ?? throw new InvalidOperationException("Yandex Market credentials are not in scope.");
    private long BusinessId => Credentials.BusinessId
        ?? throw new InvalidOperationException("Yandex Market business is not selected.");
    private long CampaignId => Credentials.CampaignId
        ?? throw new InvalidOperationException("Yandex Market campaign is not selected.");

    public async Task<IReadOnlyList<YandexMarketCampaignDto>> GetCampaignsAsync(CancellationToken ct)
    {
        var campaigns = new List<YandexMarketCampaignDto>();
        string? token = null;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        do
        {
            var response = await api.GetCampaignsAsync(token, 100, ct);
            campaigns.AddRange(response.Campaigns
                .Where(c => c.PlacementType == PlacementType.FBS
                    && c.ApiAvailability == ApiAvailabilityStatusType.AVAILABLE && c.Id > 0 && c.Business?.Id > 0)
                .Select(c => new YandexMarketCampaignDto(c.Id!.Value, c.Business!.Id!.Value,
                    c.Domain ?? Id(c.Id.Value), c.Business.Name)));
            token = Next(response.Paging?.NextPageToken, seen);
            if (token is not null) await PauseAsync(ct);
        } while (token is not null);
        return campaigns.DistinctBy(c => c.CampaignId).ToArray();
    }

    public async Task<ExternalSellerInfo> GetSellerInfoAsync(CancellationToken ct)
    {
        var campaign = (await api.GetCampaignAsync(CampaignId, ct)).Campaign
            ?? throw new InvalidOperationException("Yandex Market returned no campaign.");
        return new(campaign.Domain, null, null, null, null, HasLegalIdentity: false);
    }

    public async Task<IReadOnlyList<ExternalWarehouse>> GetWarehousesAsync(CancellationToken ct)
    {
        var result = new List<ExternalWarehouse>();
        string? token = null;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        do
        {
            var response = (await api.GetPagedWarehousesAsync(BusinessId, token, 30, null, ct)).Result
                ?? throw new InvalidOperationException("Yandex Market returned no warehouse result.");
            result.AddRange(response.Warehouses.Where(w => w.CampaignId == CampaignId)
                .Select(w => new ExternalWarehouse(Id(w.Id), w.Name,
                    w.Express ? MarketplaceWarehouseKind.Express : MarketplaceWarehouseKind.Fbs,
                    MarketplaceWarehouseStatus.Active, null, null)));
            token = Next(response.Paging?.NextPageToken, seen);
            if (token is not null) await PauseAsync(ct);
        } while (token is not null);
        return result;
    }

    public async IAsyncEnumerable<IReadOnlyList<ExternalCard>> GetCardsAsync(
        [EnumeratorCancellation] CancellationToken ct)
    {
        string? token = null;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        do
        {
            var response = (await api.GetOfferMappingsAsync(BusinessId, token, 100, CatalogLanguageType.RU, null, ct)).Result
                ?? throw new InvalidOperationException("Yandex Market returned no offer result.");
            yield return response.OfferMappings.Where(x => x.Offer is not null).Select(x =>
            {
                var o = x.Offer!;
                var campaign = o.Campaigns?.FirstOrDefault(c => c.CampaignId == CampaignId);
                return new ExternalCard(o.OfferId, x.Mapping?.MarketSku?.ToString(CultureInfo.InvariantCulture),
                    o.OfferId, o.Name ?? o.OfferId, o.Barcodes?.ToArray() ?? [], o.Pictures?.FirstOrDefault(),
                    o.BasicPrice?.Value, o.BasicPrice?.CurrencyId.ToString(),
                    o.Archived == true || campaign is null || campaign.Status == OfferCampaignStatusType.ARCHIVED);
            }).ToArray();
            token = Next(response.Paging?.NextPageToken, seen);
            if (token is not null) await PauseAsync(ct);
        } while (token is not null);
    }

    public IAsyncEnumerable<IReadOnlyList<ExternalPosting>> GetActivePostingsAsync(CancellationToken ct) =>
        ReadOrdersAsync(new GetBusinessOrdersRequest
        {
            Statuses = [OrderStatusType.PROCESSING],
        }, ct);

    public async IAsyncEnumerable<IReadOnlyList<ExternalPosting>> GetPostingsAsync(ExternalPostingQuery query,
        [EnumeratorCancellation] CancellationToken ct)
    {
        if (query.Scheme != ExternalPostingScheme.Fbs) yield break;
        var since = query.StatusChangedSince ?? query.Since;
        var windows = query.StatusChangedSince is not null ? Windows(since, query.To)
            : Windows(query.Since.AddHours(3).Date, query.To.AddHours(3).Date.AddDays(1));
        foreach (var (from, to) in windows)
        {
            var dates = query.StatusChangedSince is not null
                ? new OrderDatesFilterDTO { UpdateDateFrom = Utc(from), UpdateDateTo = Utc(to) }
                : new OrderDatesFilterDTO { CreationDateFrom = Utc(from), CreationDateTo = Utc(to) };
            await foreach (var page in ReadOrdersAsync(new GetBusinessOrdersRequest { Dates = dates }, ct))
                yield return page.Where(p => query.Statuses is null || query.Statuses.Contains(p.Status))
                    .Where(p => query.StatusChangedSince is not null || p.CreatedAt >= query.Since && p.CreatedAt <= query.To)
                    .ToArray();
        }
    }

    private async IAsyncEnumerable<IReadOnlyList<ExternalPosting>> ReadOrdersAsync(GetBusinessOrdersRequest request,
        [EnumeratorCancellation] CancellationToken ct)
    {
        request.CampaignIds = [CampaignId];
        request.Fake = false;
        string? token = null;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        do
        {
            var response = await api.GetBusinessOrdersAsync(BusinessId, token, 50, request, ct);
            yield return response.Orders.Where(o => o.CampaignId == CampaignId && !o.Fake
                    && o.Status is not (OrderStatusType.PLACING or OrderStatusType.RESERVED or OrderStatusType.UNPAID or OrderStatusType.PENDING))
                .Select(MapPosting).ToArray();
            token = Next(response.Paging?.NextPageToken, seen);
            if (token is not null) await PauseAsync(ct);
        } while (token is not null);
    }

    public async Task<IReadOnlyList<ExternalPostingStatus>> GetPostingStatusesAsync(IReadOnlyList<string> ids,
        CancellationToken ct)
    {
        var result = new List<ExternalPostingStatus>();
        foreach (var chunk in ids.Chunk(50))
        {
            await foreach (var page in ReadOrdersAsync(new GetBusinessOrdersRequest
            {
                OrderIds = chunk.Select(ParseId).ToArray(),
            }, ct))
                result.AddRange(page.Select(p => new ExternalPostingStatus(p.PostingNumber, p.Status, p.RawStatus,
                    p.RawSubstatus, p.TrackingNumber, p.Cancellation, p.Items, p.Dates)));
        }
        return result;
    }

    public async Task<DateTime?> GetEarliestPostingDateAsync(CancellationToken ct)
    {
        DateTime? earliest = null;
        var to = DateTime.UtcNow;
        for (var i = 0; i < _options.EarliestPostingProbeWindows; i++)
        {
            var from = to.AddDays(-30);
            var found = false;
            await foreach (var page in GetPostingsAsync(new(ExternalPostingScheme.Fbs, from, to), ct))
                foreach (var posting in page)
                    if (posting.CreatedAt is { } date)
                    {
                        found = true;
                        if (earliest is null || date < earliest) earliest = date;
                    }
            if (!found && earliest is not null) break;
            to = from;
        }
        return earliest;
    }

    public ExternalPosting MapPosting(BusinessOrderDTO order)
    {
        var items = order.Items.Select(i => new ExternalPostingItem(null, i.OfferId, i.OfferName, i.Count,
            CustomerPrice: i.Prices?.Payment is { } paid ? (paid.Value + (i.Prices.Cashback?.Value ?? 0)) / PositiveCount(i.Count) : null,
            CustomerCurrencyCode: i.Prices?.Payment?.CurrencyId.ToString(),
            Price: i.Prices?.Payment is { } payment ? (payment.Value + (i.Prices.Cashback?.Value ?? 0)
                + (i.Prices.Subsidy?.Value ?? 0)) / PositiveCount(i.Count) : null,
            CurrencyCode: i.Prices?.Payment?.CurrencyId.ToString(), ExternalItemId: Id(i.Id))).ToArray();
        var index = order.Items.Select((item, i) => (item.Id, Index: i)).ToDictionary(x => x.Id, x => x.Index);
        var parcels = order.Delivery.BoxesLayout?.Select(box => new ExternalParcel(Id(box.BoxId), box.Barcode,
            box.Items.Select(i => new ExternalParcelItem(index.TryGetValue(i.Id, out var value) ? value
                    : throw new InvalidOperationException("Yandex Market parcel references an unknown order item."),
                i.PartialCount is null ? i.FullCount : null, i.PartialCount?.Current, i.PartialCount?.Total)).ToArray()))
            .ToArray();
        var status = NormalizeStatus(order.Status, order.Substatus);
        var reason = order.Substatus.ToString();
        var cancellation = status == MarketplaceOrderStatus.Cancelled
            ? new ExternalCancellation(null, MarketplaceCancellationType.Unknown, reason, null, LossReason(reason), reason)
            : null;
        return new(Id(order.OrderId), null, status, order.Status.ToString(), order.Substatus.ToString(),
            order.Delivery.WarehouseId, order.Delivery.ServiceName, order.Delivery.Shipment?.ShipmentDate.UtcDateTime,
            order.CreationDate.UtcDateTime, order.Delivery.Tracks?.FirstOrDefault()?.TrackCode,
            parcels?.Length ?? 1, cancellation, items, CreatedAt: order.CreationDate.UtcDateTime, Parcels: parcels);
    }

    public MarketplaceOrderStatus NormalizeStatus(OrderStatusType status, OrderSubstatusType substatus)
    {
        var normalized = status switch
        {
            OrderStatusType.PROCESSING when substatus == OrderSubstatusType.READY_TO_SHIP => MarketplaceOrderStatus.AwaitingDeliver,
            OrderStatusType.PROCESSING when substatus == OrderSubstatusType.SHIPPED => MarketplaceOrderStatus.Delivering,
            OrderStatusType.PROCESSING => MarketplaceOrderStatus.AwaitingPackaging,
            OrderStatusType.DELIVERY or OrderStatusType.PICKUP => MarketplaceOrderStatus.Delivering,
            OrderStatusType.DELIVERED or OrderStatusType.PARTIALLY_RETURNED or OrderStatusType.RETURNED => MarketplaceOrderStatus.Delivered,
            OrderStatusType.CANCELLED => MarketplaceOrderStatus.Cancelled,
            _ => MarketplaceOrderStatus.Unknown,
        };
        if (normalized == MarketplaceOrderStatus.Unknown)
            logger.LogWarning("Unknown Yandex Market order status {Status}/{Substatus}", status, substatus);
        return normalized;
    }

    public static MarketplaceLossReason LossReason(string? reason, string? subreason = null) => (reason, subreason) switch
    {
        (_, "INCOMPLETENESS") => MarketplaceLossReason.Incomplete,
        ("USER_CHANGED_MIND" or "USER_PLACED_OTHER_ORDER" or "REPLACING_ORDER", _) => MarketplaceLossReason.BuyerChangedMind,
        ("USER_NOT_PAID", _) => MarketplaceLossReason.BuyerOther,
        ("USER_BOUGHT_CHEAPER", _) => MarketplaceLossReason.BuyerFoundCheaper,
        ("USER_REFUSED_DELIVERY", _) => MarketplaceLossReason.BuyerDeliveryTerms,
        ("USER_REFUSED_PRODUCT" or "DOES_NOT_FIT", _) => MarketplaceLossReason.BuyerDidNotFit,
        ("PICKUP_EXPIRED", _) => MarketplaceLossReason.BuyerNotPickedUp,
        ("SHOP_FAILED", _) => MarketplaceLossReason.SellerCancelled,
        ("PROCESSING_EXPIRED", _) => MarketplaceLossReason.NotShippedInTime,
        ("MISSING_ITEM", _) => MarketplaceLossReason.OutOfStock,
        ("BROKEN_ITEM" or "BAD_QUALITY", _) => MarketplaceLossReason.Defect,
        ("USER_REFUSED_QUALITY", _) => MarketplaceLossReason.PoorQuality,
        ("WRONG_ITEM", _) => MarketplaceLossReason.WrongItem,
        _ => MarketplaceLossReason.Unknown,
    };

    private Task PauseAsync(CancellationToken ct) => Task.Delay(_options.PageDelayMs, ct);
    private static int PositiveCount(int count) => count > 0 ? count
        : throw new InvalidDataException("Yandex Market order item count must be positive.");
    private static string Id(long id) => id.ToString(CultureInfo.InvariantCulture);
    private static long ParseId(string id) => long.Parse(id, CultureInfo.InvariantCulture);
    private static DateTimeOffset Utc(DateTime date) => new(DateTime.SpecifyKind(date, DateTimeKind.Utc));
    private static string? Next(string? token, HashSet<string> seen) => string.IsNullOrEmpty(token) ? null
        : seen.Add(token) ? token : throw new InvalidOperationException("Yandex Market repeated a pagination token.");

    public static IEnumerable<(DateTime From, DateTime To)> Windows(DateTime from, DateTime to)
    {
        while (from <= to)
        {
            var end = from.AddDays(29) < to ? from.AddDays(29) : to;
            yield return (from, end);
            if (end == to) yield break;
            from = end;
        }
    }
}
