using System.Globalization;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Options;
using Neisvestney.Marketplaces.Wildberries;
using Polly.Timeout;
using ProjectWarehouse.Server.Domain;
using ProjectWarehouse.Server.Infrastructure.Labels;
using ProjectWarehouse.Server.Infrastructure.Marketplaces;
using ProjectWarehouse.Server.Integrations.Abstractions;

namespace ProjectWarehouse.Server.Integrations.Wildberries;

public class WildberriesClient(
    IWildberriesCommonClient common,
    IWildberriesContentClient content,
    IWildberriesMarketplaceClient marketplace,
    IWildberriesSellerAnalyticsClient analytics,
    IWildberriesFinanceClient finance,
    IWildberriesStatisticsClient statistics,
    IOptions<MarketplacesOptions> options,
    ILogger<WildberriesClient> logger) : IWildberriesClient
{
    private const string MoscowZoneId = "Europe/Moscow";
    private static readonly TimeZoneInfo MoscowZone = TimeZoneInfo.FindSystemTimeZoneById(MoscowZoneId);

    private const int CardsPageSize = 100;
    private const int OrdersPageSize = 1000;
    private const int StatusBatchSize = 1000;
    private const int ArchivePageSize = 1000;
    private const int SuppliesPageSize = 1000;
    private const int ReturnsPageSize = 1000;
    private const int ReportTransportRetries = 2;

    /// <summary>WB cuts a statistics page at about 80 000 rows; a page well short of that is the last one.</summary>
    private const int StatisticsLastPageBelow = 50_000;

    private const string WbWarehouseType = "Склад WB";

    /// <summary>Realization report's <c>deliveryMethod</c> of a unit sold from a WB warehouse, e.g. «FBW, (МГТ, короба)».</summary>
    private const string FbwDeliveryMethod = "FBW";

    /// <summary>/api/v3/orders rejects a creation window longer than 30 days.</summary>
    private static readonly TimeSpan OrdersWindow = TimeSpan.FromDays(30) - TimeSpan.FromSeconds(1);

    /// <summary>How many archive months the earliest-posting probe walks back.</summary>
    private const int EarliestProbeMonths = 36;

    /// <summary>How many archive months before a return's orderDt its task is looked for; up to 108 days were seen.</summary>
    private const int ReturnTaskLookbackMonths = 6;

    private const double StickerWidthMm = 58;
    private const double StickerHeightMm = 40;

    private static readonly HashSet<string> CancelledWbStatuses =
        ["canceled", "canceled_by_client", "declined_by_client", "defect", "canceled_by_carrier", "cancel_carrier"];

    // ISO 4217 numeric codes WB prices orders in
    private static readonly Dictionary<long, string> Currencies = new()
    {
        [643] = "RUB", [933] = "BYN", [398] = "KZT", [51] = "AMD", [417] = "KGS", [860] = "UZS",
    };

    private readonly WildberriesOptions _options = options.Value.Wildberries;

    // the backfill's FBW reconstruction reads the realization report anyway; its accrual step replays that read
    private (DateOnly First, DateOnly Last, List<IReadOnlyList<ExternalAccrual>> Pages)? _reportAccruals;

    public async Task PingAsync(CancellationToken ct) => await common.GetV1SellerInfoAsync(ct);

    public async Task<ExternalSellerInfo> GetSellerInfoAsync(CancellationToken ct)
    {
        var info = await common.GetV1SellerInfoAsync(ct);
        return new ExternalSellerInfo(
            Name: NullIfEmpty(info.TradeMark) ?? NullIfEmpty(info.Name),
            LegalName: NullIfEmpty(info.Name),
            Inn: NullIfEmpty(info.Tin),
            Ogrn: null,
            OwnershipForm: null);
    }

    public async Task<IReadOnlyList<ExternalWarehouse>> GetWarehousesAsync(CancellationToken ct)
    {
        var warehouses = await marketplace.GetV3WarehousesAsync(ct);
        return warehouses
            .Where(w => w.Id is not null)
            .Select(w => new ExternalWarehouse(
                w.Id!.Value.ToString(CultureInfo.InvariantCulture),
                w.Name ?? "",
                w.DeliveryType switch
                {
                    WarehouseDeliveryType._1 => MarketplaceWarehouseKind.Fbs,
                    WarehouseDeliveryType._2 or WarehouseDeliveryType._6 => MarketplaceWarehouseKind.Rfbs,
                    _ => MarketplaceWarehouseKind.Unknown,
                },
                w.IsDeleting == true ? MarketplaceWarehouseStatus.Inactive : MarketplaceWarehouseStatus.Active,
                w.IsDeleting == true ? "deleting" : null,
                null))
            .ToList();
    }

    public async IAsyncEnumerable<IReadOnlyList<ExternalCard>> GetCardsAsync([EnumeratorCancellation] CancellationToken ct)
    {
        var cursor = new Cursor3 { Limit = CardsPageSize };
        while (true)
        {
            var page = await content.PostV2GetCardsListAsync(null, new PostV2GetCardsListRequest
            {
                Settings = new Settings { Cursor = cursor },
            }, ct);

            var cards = (page.Cards ?? [])
                .SelectMany(c => MapSizes(c.VendorCode, c.Title, c.Photos?.FirstOrDefault()?.Big, c.Sizes?
                    .Select(s => (s.ChrtID, s.TechSize, s.Skus)), false))
                .ToList();
            if (cards.Count > 0)
                yield return cards;

            if (page.Cursor is not { } next || (next.Total ?? 0) < CardsPageSize)
                break;
            cursor = new Cursor3 { Limit = CardsPageSize, UpdatedAt = next.UpdatedAt, NmID = next.NmID };
            await Task.Delay(_options.PageDelayMs, ct);
        }

        var trashCursor = new Cursor4 { Limit = CardsPageSize };
        while (true)
        {
            var page = await content.PostV2GetCardsTrashAsync(null, new PostV2GetCardsTrashRequest
            {
                Settings = new Settings2 { Cursor = trashCursor },
            }, ct);

            // a trashed card carries no title, so the article names it
            var cards = (page.Cards ?? [])
                .SelectMany(c => MapSizes(c.VendorCode, null, c.Photos?.FirstOrDefault()?.Big, c.Sizes?
                    .Select(s => (s.ChrtID, s.TechSize, s.Skus)), true))
                .ToList();
            if (cards.Count > 0)
                yield return cards;

            if (page.Cursor is not { } next || (next.Total ?? 0) < CardsPageSize)
                break;
            trashCursor = new Cursor4 { Limit = CardsPageSize, TrashedAt = next.TrashedAt, NmID = next.NmID };
            await Task.Delay(_options.PageDelayMs, ct);
        }
    }

    private static IEnumerable<ExternalCard> MapSizes(string? vendorCode, string? title, string? imageUrl,
        IEnumerable<(ulong? ChrtId, string? TechSize, IReadOnlyList<string>? Skus)>? sizes, bool isArchived)
    {
        var offerId = vendorCode ?? "";
        var name = NullIfEmpty(title) ?? offerId;
        foreach (var size in sizes ?? [])
        {
            if (size.ChrtId is not { } chrtId)
                continue;

            var id = chrtId.ToString(CultureInfo.InvariantCulture);
            // "0" is how WB spells a product without sizes
            var sized = size.TechSize is { Length: > 0 } techSize && techSize != "0" ? $"{name} ({techSize})" : name;
            yield return new ExternalCard(id, id, offerId, sized, size.Skus ?? [], imageUrl, null, null, isArchived);
        }
    }

    public async IAsyncEnumerable<IReadOnlyList<ExternalPosting>> GetActivePostingsAsync(
        [EnumeratorCancellation] CancellationToken ct)
    {
        var to = DateTimeOffset.UtcNow;
        var since = to.AddDays(-_options.ActiveTasksWindowDays);
        var supplies = NewSupplyDates();

        await foreach (var tasks in ListLiveTasksAsync(since, to, ct))
        {
            var statuses = await GetRawStatusesAsync(tasks.Select(t => t.Id).ToList(), ct);
            var postings = new List<ExternalPosting>();
            foreach (var task in tasks)
            {
                if (!statuses.TryGetValue(task.Id, out var status)
                    || status.Supplier is not ("new" or "confirm") || IsCancelled(status.Supplier, status.Wb))
                    continue;
                postings.Add(await MapPostingAsync(task with { Supplier = status.Supplier, Wb = status.Wb }, supplies, ct));
            }

            if (postings.Count > 0)
                yield return postings;
        }
    }

    public async IAsyncEnumerable<IReadOnlyList<ExternalPosting>> GetPostingsAsync(ExternalPostingQuery query,
        [EnumeratorCancellation] CancellationToken ct)
    {
        if (query.Scheme == ExternalPostingScheme.Fbo)
        {
            await foreach (var page in GetFboPostingsAsync(query, ct))
                yield return page;
            yield break;
        }

        var since = new DateTimeOffset(DateTime.SpecifyKind(query.Since, DateTimeKind.Utc));
        var to = new DateTimeOffset(DateTime.SpecifyKind(query.To, DateTimeKind.Utc));
        var supplies = NewSupplyDates();
        var seen = new HashSet<long>();

        // the archive first: the live list still holds finished tasks for months, but only the archive states
        // their sticker number; StatusChangedSince is not narrowed, a superset is fine
        foreach (var month in Months(since, to))
        {
            await foreach (var tasks in ListArchiveAsync(month, ct))
            {
                var postings = await MapMatchingAsync(tasks
                    .Where(t => t.CreatedAt is not { } created || (created >= since.UtcDateTime && created <= to.UtcDateTime))
                    .Where(t => seen.Add(t.Id)), query, supplies, ct);
                if (postings.Count > 0)
                    yield return postings;
            }
        }

        await foreach (var tasks in ListLiveTasksAsync(since, to, ct))
        {
            var fresh = tasks.Where(t => seen.Add(t.Id)).ToList();
            if (fresh.Count == 0)
                continue;
            var statuses = await GetRawStatusesAsync(fresh.Select(t => t.Id).ToList(), ct);
            var postings = await MapMatchingAsync(fresh
                .Where(t => statuses.ContainsKey(t.Id))
                .Select(t => t with { Supplier = statuses[t.Id].Supplier, Wb = statuses[t.Id].Wb }), query, supplies, ct);
            if (postings.Count > 0)
                yield return postings;
        }
    }

    private async Task<List<ExternalPosting>> MapMatchingAsync(IEnumerable<AssemblyTask> tasks,
        ExternalPostingQuery query, SupplyDates supplies, CancellationToken ct)
    {
        var postings = new List<ExternalPosting>();
        foreach (var task in tasks)
        {
            var posting = await MapPostingAsync(task, supplies, ct);
            if (query.Statuses is null || query.Statuses.Contains(posting.Status))
                postings.Add(posting);
        }

        return postings;
    }

    public async Task<IReadOnlyList<ExternalPostingStatus>> GetPostingStatusesAsync(
        IReadOnlyList<string> postingNumbers, CancellationToken ct)
    {
        var ids = postingNumbers
            .Select(n => long.TryParse(n, NumberStyles.None, CultureInfo.InvariantCulture, out var id) ? id : (long?)null)
            .OfType<long>()
            .Distinct()
            .ToList();
        var statuses = await GetRawStatusesAsync(ids, ct);

        // the status call names no supply, so the handover date of a task in delivery is looked up among live tasks
        var supplyIds = new Dictionary<long, string>();
        if (statuses.Values.Any(s => s.Supplier == "complete"))
        {
            var to = DateTimeOffset.UtcNow;
            await foreach (var tasks in ListLiveTasksAsync(to.AddDays(-_options.ActiveTasksWindowDays), to, ct))
                foreach (var task in tasks.Where(t => t.SupplyId is { Length: > 0 } && statuses.ContainsKey(t.Id)))
                    supplyIds[task.Id] = task.SupplyId!;
        }

        var supplies = NewSupplyDates();
        var result = new List<ExternalPostingStatus>(statuses.Count);
        foreach (var (id, status) in statuses)
        {
            var deliveringAt = status.Supplier == "complete" && supplyIds.TryGetValue(id, out var supplyId)
                ? await supplies.GetAsync(supplyId, ct)
                : null;
            var number = id.ToString(CultureInfo.InvariantCulture);
            result.Add(new ExternalPostingStatus(
                number,
                MapStatus(number, status.Supplier, status.Wb),
                status.Supplier,
                status.Wb,
                null,
                MapCancellation(status.Supplier, status.Wb),
                [],
                Dates(deliveringAt)));
        }

        return result;
    }

    /// <summary>
    /// FBW orders have no assembly task: the statistics report names each unit by <c>srid</c>, which is also the
    /// rid the realization report files its rows under. flag=0 lists rows changed since dateFrom, a superset of the
    /// orders placed in the period and of every sale of them.
    /// </summary>
    private async IAsyncEnumerable<IReadOnlyList<ExternalPosting>> GetFboPostingsAsync(ExternalPostingQuery query,
        [EnumeratorCancellation] CancellationToken ct)
    {
        var rows = await ReadOrdersAsync(query.Since, ct);
        var orders = LatestFbo(rows)
            .Where(o => ParseMoscowDate(o.Date) is { } placed && placed >= query.Since && placed <= query.To)
            .ToList();

        CardIndex? cards = null;
        if (orders.Count > 0)
        {
            var sales = await ReadSalesAsync(query.Since, ct);
            cards = await CardIndex.LoadAsync(this, ct);
            foreach (var chunk in orders.Chunk(OrdersPageSize))
            {
                var postings = chunk
                    .Select(o => MapFboPosting(o, sales[o.Srid!], cards))
                    .Where(p => query.Statuses is null || query.Statuses.Contains(p.Status))
                    .ToList();
                if (postings.Count > 0)
                    yield return postings;
            }
        }

        if (!query.IncludeHistory)
            yield break;

        // statistics speaks for every unit it still keeps, FBS ones included
        var known = rows.Select(o => o.Srid).OfType<string>().ToHashSet(StringComparer.Ordinal);
        cards ??= await CardIndex.LoadAsync(this, ct);
        var reconstructed = (await ReadReportUnitsAsync(query, cards, ct))
            .Where(unit => unit.IsFbw && !known.Contains(unit.Srid))
            .Select(unit => MapReconstructedFboPosting(unit, cards))
            .Where(p => p.InProcessAt is { } placed && placed >= query.Since && placed <= query.To)
            .Where(p => query.Statuses is null || query.Statuses.Contains(p.Status))
            .ToList();
        foreach (var chunk in reconstructed.Chunk(OrdersPageSize))
            yield return chunk;
    }

    /// <summary>
    /// Every <c>srid</c> of the realization report, folded row by row. A unit is filed under the report of each of
    /// its financial events, so reports are read from the backfill's accrual range start to today; the accruals of
    /// the read within that range are kept for <see cref="GetAccrualsAsync"/> of the same backfill.
    /// </summary>
    private async Task<IEnumerable<ReportUnit>> ReadReportUnitsAsync(ExternalPostingQuery query, CardIndex cards,
        CancellationToken ct)
    {
        var (first, last) = AccrualRange(DateOnly.FromDateTime(query.Since), DateOnly.FromDateTime(query.To));
        var units = new Dictionary<string, ReportUnit>(StringComparer.Ordinal);
        var accruals = new List<IReadOnlyList<ExternalAccrual>>();
        await foreach (var rows in ReadReportAsync(first, MoscowToday(), ct))
        {
            foreach (var row in rows)
            {
                // service rows (storage, penalties) carry a bare number in srid and belong to no unit
                if (NullIfEmpty(row.Srid) is not { } srid || srid.All(char.IsAsciiDigit))
                    continue;
                if (!units.TryGetValue(srid, out var unit))
                    units[srid] = unit = new ReportUnit(srid);
                unit.Add(row);
            }
            var page = MapAccruals(rows, cards).Where(a => a.Date <= last).ToList();
            if (page.Count > 0)
                accruals.Add(page);
        }

        _reportAccruals = (first, last, accruals);
        return units.Values;
    }

    /// <summary>
    /// The report keeps no order status: more sales than reversals make the unit delivered, otherwise it is taken
    /// for a refusal. A sale reversed in full is the accrual sync's business, the same as for statistics.
    /// </summary>
    private static ExternalPosting MapReconstructedFboPosting(ReportUnit unit, CardIndex cards)
    {
        var card = cards.ByBarcode(unit.Barcode);
        var offerId = card?.VendorCode ?? unit.VendorCode ?? "";
        var sold = unit.Sales > unit.Reversals;
        var item = new ExternalPostingItem(
            card?.ChrtId,
            offerId,
            offerId,
            1,
            Price: unit.Price,
            CurrencyCode: unit.Currency ?? "RUB");

        return new ExternalPosting(
            unit.Srid,
            unit.OrderUid,
            sold ? MarketplaceOrderStatus.Delivered : MarketplaceOrderStatus.Cancelled,
            null,
            unit.DeliveryMethod,
            null,
            null,
            null,
            unit.PlacedAt,
            null,
            1,
            sold ? null : new ExternalCancellation(null, MarketplaceCancellationType.Unknown, null, null, null, null),
            [item],
            ExternalPostingScheme.Fbo,
            unit.PlacedAt,
            Rid: unit.Srid,
            IsReconstructed: true);
    }

    /// <summary>One <c>srid</c> of the realization report, reduced to what reconstruction reads.</summary>
    private sealed class ReportUnit(string srid)
    {
        public string Srid { get; } = srid;
        public string? OrderUid { get; private set; }
        public string? Barcode { get; private set; }
        public string? VendorCode { get; private set; }
        public string? DeliveryMethod { get; private set; }
        public DateTime? PlacedAt { get; private set; }
        public int Sales { get; private set; }
        public int Reversals { get; private set; }

        /// <summary>The sale's price, else the first one any row states.</summary>
        public decimal? Price => _salePrice ?? _anyPrice;
        public string? Currency => _salePrice is not null ? _saleCurrency : _anyCurrency;

        // service rows carry a bare number in srid and no order, and FBS rows come without orderId now and then;
        // a return shipped back to the seller gets an srid of its own and travels to the seller, never a customer
        public bool IsFbw => !_hasOrderId && OrderUid is not null && _fbw && !_notFbw
                             && (Sales > 0 || _toCustomer) && !_toSeller;

        private bool _hasOrderId, _fbw, _notFbw, _toCustomer, _toSeller;
        private decimal? _salePrice, _anyPrice;
        private string? _saleCurrency, _anyCurrency;

        public void Add(SalesReportsDetailedRes row)
        {
            _hasOrderId |= row.OrderId != 0;
            OrderUid ??= NullIfEmpty(row.OrderUid);
            Barcode ??= NullIfEmpty(row.Sku);
            VendorCode ??= NullIfEmpty(row.VendorCode);
            if (NullIfEmpty(row.DeliveryMethod) is { } method)
            {
                DeliveryMethod ??= method;
                if (method.StartsWith(FbwDeliveryMethod, StringComparison.Ordinal))
                    _fbw = true;
                else
                    _notFbw = true;
            }
            _toCustomer |= IsToCustomer(row.BonusTypeName);
            _toSeller |= IsToSeller(row.BonusTypeName);
            if (row.OrderDt != default && (PlacedAt is null || row.OrderDt.UtcDateTime < PlacedAt))
                PlacedAt = row.OrderDt.UtcDateTime;

            var price = Money(row.RetailPriceWithDisc);
            var currency = NullIfEmpty(row.Currency);
            if (price > 0 && _anyPrice is null)
                (_anyPrice, _anyCurrency) = (price, currency);
            if (Money(row.RetailAmount) <= 0)
                return;
            // a sale storno and a customer return both come as «Возврат»
            if (row.DocTypeName == "Продажа")
            {
                Sales++;
                if (price > 0 && _salePrice is null)
                    (_salePrice, _saleCurrency) = (price, currency);
            }
            else if (row.DocTypeName == "Возврат")
            {
                Reversals++;
            }
        }
    }

    public async Task<IReadOnlyList<ExternalPostingStatus>> GetFboPostingStatusesAsync(
        IReadOnlyList<string> postingNumbers, CancellationToken ct)
    {
        // the report has no filter by srid, so the open ones are picked out of everything changed lately
        var since = DateTime.UtcNow.AddDays(-_options.FboStatusLookbackDays);
        var wanted = postingNumbers.ToHashSet(StringComparer.Ordinal);
        var orders = LatestFbo(await ReadOrdersAsync(since, ct)).Where(o => wanted.Contains(o.Srid!)).ToList();
        if (orders.Count == 0)
            return [];

        var sales = await ReadSalesAsync(since, ct);
        return orders
            .Select(o =>
            {
                var (status, cancellation) = MapFboStatus(o, sales[o.Srid!]);
                return new ExternalPostingStatus(o.Srid!, status, null, null, null, cancellation, []);
            })
            .ToList();
    }

    private Task<List<OrdersItem>> ReadOrdersAsync(DateTime since, CancellationToken ct) =>
        ReadStatisticsAsync(statistics.GetV1SupplierOrdersAsync, o => o.LastChangeDate, since, ct);

    /// <summary>The latest row of every unit shipped from a WB warehouse.</summary>
    private static List<OrdersItem> LatestFbo(IEnumerable<OrdersItem> rows) => rows
        .Where(o => o.WarehouseType == WbWarehouseType && !string.IsNullOrEmpty(o.Srid))
        .GroupBy(o => o.Srid!)
        // ISO wall time compares as text
        .Select(g => g.MaxBy(o => o.LastChangeDate, StringComparer.Ordinal)!)
        .ToList();

    private async Task<ILookup<string, SalesItem>> ReadSalesAsync(DateTime since, CancellationToken ct) =>
        (await ReadStatisticsAsync(statistics.GetV1SupplierSalesAsync, s => s.LastChangeDate, since, ct))
        .Where(s => !string.IsNullOrEmpty(s.Srid))
        .ToLookup(s => s.Srid!);

    private async Task<List<T>> ReadStatisticsAsync<T>(
        Func<string, long?, CancellationToken, Task<IReadOnlyList<T>>> call, Func<T, string?> lastChangeDate,
        DateTime since, CancellationToken ct)
    {
        var rows = new List<T>();
        var dateFrom = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(since, DateTimeKind.Utc), MoscowZone)
            .ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture);
        while (true)
        {
            var page = await call(dateFrom, 0, ct);
            rows.AddRange(page);
            // the next page starts at the last row's lastChangeDate, passed verbatim
            if (page.Count < StatisticsLastPageBelow)
                return rows;
            if (lastChangeDate(page[^1]) is not { Length: > 0 } next || next == dateFrom)
            {
                logger.LogWarning("Wildberries statistics page of {Count} rows ends where it started, at {DateFrom}; "
                                  + "the rows past it are not read", page.Count, dateFrom);
                return rows;
            }
            dateFrom = next;
        }
    }

    private static ExternalPosting MapFboPosting(OrdersItem order, IEnumerable<SalesItem> sales, CardIndex cards)
    {
        var card = cards.ByBarcode(order.Barcode);
        var offerId = card?.VendorCode ?? order.SupplierArticle ?? "";
        var (status, cancellation) = MapFboStatus(order, sales);
        var placed = ParseMoscowDate(order.Date);
        var item = new ExternalPostingItem(
            card?.ChrtId,
            offerId,
            offerId,
            1,
            CustomerPrice: order.FinishedPrice,
            CustomerCurrencyCode: "RUB",
            Price: order.PriceWithDisc,
            CurrencyCode: "RUB");

        return new ExternalPosting(
            order.Srid!,
            NullIfEmpty(order.GNumber),
            status,
            null,
            null,
            null,
            null,
            null,
            placed,
            null,
            1,
            cancellation,
            [item],
            ExternalPostingScheme.Fbo,
            placed,
            Rid: order.Srid);
    }

    /// <summary>
    /// The report keeps no status: a sale (<c>S…</c>) not reversed by a sale storno (<c>A…</c>) makes the unit
    /// delivered, whatever came after it — a return is the returns sync's business; otherwise a cancelled order is
    /// cancelled, and an open one is on its way.
    /// </summary>
    private static (MarketplaceOrderStatus, ExternalCancellation?) MapFboStatus(OrdersItem order,
        IEnumerable<SalesItem> sales)
    {
        var ids = sales.Select(s => s.SaleID).OfType<string>().ToList();
        if (ids.Count(id => id.StartsWith('S')) > ids.Count(id => id.StartsWith('A')))
            return (MarketplaceOrderStatus.Delivered, null);
        if (order.IsCancel == true)
            // the report does not say who cancelled
            return (MarketplaceOrderStatus.Cancelled,
                new ExternalCancellation(null, MarketplaceCancellationType.Unknown, null, null, null, null));
        return (MarketplaceOrderStatus.Delivering, null);
    }

    public async IAsyncEnumerable<IReadOnlyList<ExternalReturn>> GetReturnsAsync(ExternalReturnQuery query,
        [EnumeratorCancellation] CancellationToken ct)
    {
        // WB keeps no compensation status, and the report has no change date to stream by
        if (query.Since is not { } since)
            yield break;

        var today = MoscowToday();
        var from = DateOnly.FromDateTime(since);
        var lookback = today.AddDays(-_options.ReturnsLookbackDays);
        if (lookback < from)
            from = lookback;

        var rows = new List<Report>();
        for (var offset = 0L; ; offset += ReturnsPageSize)
        {
            var page = await analytics.GetV1GoodsReturnAsync(
                new DateTimeOffset(from.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero),
                new DateTimeOffset(today.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero),
                null, ReturnsPageSize, offset, ct);
            rows.AddRange(page.Report);
            if (page.Report.Count < ReturnsPageSize)
                break;
            await Task.Delay(_options.PageDelayMs, ct);
        }

        // each attempt to send a unit back is a row of its own; the unit keeps the latest
        var latest = rows
            .Where(r => r.ShkId > 0 || !string.IsNullOrEmpty(r.Srid))
            .GroupBy(r => r.ShkId > 0 ? r.ShkId.ToString(CultureInfo.InvariantCulture) : r.Srid)
            .Select(g => g.MaxBy(r => (ParseDate(r.ReadyToReturnDt), ParseDay(r.OrderDt)))!)
            .ToList();
        if (latest.Count == 0)
            yield break;

        // the unit's shkId is the sticker of its task, which only the archive states; orderDt dates the trip back,
        // months after the task. Only the history import pays for that read: a background pass leaves the posting
        // to the sync, which finds it by the stored sticker or journal row.
        var tasksBySticker = new Dictionary<long, AssemblyTask>();
        var days = latest.Select(r => ParseDay(r.OrderDt)).OfType<DateOnly>().ToList();
        if (query.DateKind == ExternalReturnDateKind.Returned && days.Count > 0)
        {
            var (first, last) = (days.Min(), days.Max());
            for (var month = new DateOnly(first.Year, first.Month, 1).AddMonths(-ReturnTaskLookbackMonths);
                 month <= last; month = month.AddMonths(1))
                await foreach (var tasks in ListArchiveAsync(month, ct))
                    foreach (var task in tasks.Where(t => t.StickerId is not null))
                        tasksBySticker[task.StickerId!.Value] = task;
        }

        var cards = await CardIndex.LoadAsync(this, ct);
        foreach (var chunk in latest.Chunk(ReturnsPageSize))
            yield return chunk.Select(r => MapReturn(r, tasksBySticker, cards)).ToList();
    }

    private static ExternalReturn MapReturn(Report row, Dictionary<long, AssemblyTask> tasksBySticker, CardIndex cards)
    {
        var task = tasksBySticker.GetValueOrDefault(row.ShkId);
        var card = cards.ByBarcode(row.Sku);
        var sku = task?.ChrtId?.ToString(CultureInfo.InvariantCulture) ?? card?.ChrtId;
        var completedAt = ParseDate(row.CompletedDt);
        var readyAt = ParseDate(row.ReadyToReturnDt);

        return new ExternalReturn(
            ExternalId: row.ShkId > 0 ? row.ShkId.ToString(CultureInfo.InvariantCulture) : row.Srid,
            PostingNumber: task?.Id.ToString(CultureInfo.InvariantCulture) ?? "",
            Sku: sku,
            OfferId: task?.Article ?? card?.VendorCode ?? "",
            Scheme: task is null ? MarketplaceReturnScheme.Unknown : MarketplaceReturnScheme.Fbs,
            RawScheme: null,
            // the task is already counted as cancelled; a unit the journal shows sold is made a customer return there
            Kind: MarketplaceReturnKind.FullRefusal,
            RawKind: NullIfEmpty(row.ReturnType),
            Quantity: 1,
            Price: null,
            CurrencyCode: null,
            Reason: NullIfEmpty(row.ReturnReason) ?? NullIfEmpty(row.ReturnType),
            ReasonCategory: row.ReturnType switch
            {
                "Возврат брака" => MarketplaceLossReason.Defect,
                "Возврат неверного вложения" => MarketplaceLossReason.WrongItem,
                _ => null,
            },
            RawReasonCode: null,
            RawStatus: NullIfEmpty(row.ReturnStatus),
            StatusName: NullIfEmpty(row.ReturnStatus),
            IsCancelled: row.ReturnStatus?.StartsWith("Отмена", StringComparison.OrdinalIgnoreCase) == true,
            StatusChangedAt: completedAt ?? readyAt,
            ReturnedAt: readyAt,
            FinalAt: completedAt ?? ParseDate(row.ExpiredDt),
            CompensationStatus: null,
            CompensationStatusChangedAt: null,
            SourceExternalId: NullIfEmpty(row.Srid),
            ExemplarId: row.ShkId > 0 ? row.ShkId.ToString(CultureInfo.InvariantCulture) : null);
    }

    public async IAsyncEnumerable<IReadOnlyList<ExternalAccrual>> GetAccrualsAsync(DateOnly from, DateOnly to,
        [EnumeratorCancellation] CancellationToken ct)
    {
        var (first, last) = AccrualRange(from, to);
        if (first > last)
            yield break;

        var read = _reportAccruals;
        _reportAccruals = null;
        if (read is { } cached && cached.First <= first && cached.Last >= last)
        {
            foreach (var page in cached.Pages)
                yield return page;
            yield break;
        }

        CardIndex? cards = null;
        await foreach (var rows in ReadReportAsync(first, last, ct))
        {
            cards ??= await CardIndex.LoadAsync(this, ct);
            if (MapAccruals(rows, cards) is { Count: > 0 } accruals)
                yield return accruals;
        }
    }

    /// <summary>Report dates an accrual read of the period covers: the lookback below it, nothing past today.</summary>
    private (DateOnly First, DateOnly Last) AccrualRange(DateOnly from, DateOnly to)
    {
        var today = MoscowToday();
        var last = to > today ? today : to;
        var lookback = last.AddDays(-_options.AccrualsLookbackDays);
        return (lookback < from ? lookback : from, last);
    }

    private List<ExternalAccrual> MapAccruals(IReadOnlyList<SalesReportsDetailedRes> rows, CardIndex cards)
    {
        var accruals = new List<ExternalAccrual>();
        foreach (var row in rows)
        {
            // a row without its own date belongs to the report period it came in
            if ((ParseDay(row.RrDate) ?? ParseDay(row.DateTo)) is not { } day)
            {
                logger.LogWarning("Wildberries report row {RrdId} carries no date, skipped", row.RrdId);
                continue;
            }
            var accrual = MapAccrual(row, day, cards);
            if (accrual.Lines.Count > 0)
                accruals.Add(accrual);
        }
        return accruals;
    }

    /// <summary>Realization report rows of the reports dated within the period, page by page.</summary>
    private async IAsyncEnumerable<IReadOnlyList<SalesReportsDetailedRes>> ReadReportAsync(DateOnly first,
        DateOnly last, [EnumeratorCancellation] CancellationToken ct)
    {
        long rrdId = 0;
        var transportFailures = 0;
        while (true)
        {
            IReadOnlyList<SalesReportsDetailedRes> rows;
            // the body is read past the resilience handler, so its timeouts do not cover a stalled transfer
            using var pageTimeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            pageTimeout.CancelAfter(TimeSpan.FromSeconds(_options.ReportTimeoutSeconds));
            try
            {
                rows = await finance.PostV1SalesReportsDetailedAsync(new SalesReportsDetailedReq
                {
                    DateFrom = first.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    DateTo = last.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    Limit = _options.ReportPageSize,
                    RrdId = rrdId,
                    Period = "weekly",
                }, pageTimeout.Token);
            }
            catch (WildberriesApiException ex) when (ex.StatusCode == StatusCodes.Status204NoContent)
            {
                // 204 is how the report says there are no more rows
                yield break;
            }
            // the resilience handler does not retry this client: an immediate retry would hit the
            // one-call-a-minute quota, so a failed page is asked again here, after the report pause
            catch (Exception ex) when (transportFailures < ReportTransportRetries && IsTransientReportFailure(ex, ct))
            {
                transportFailures++;
                logger.LogWarning(ex, "Wildberries report page after rrdId {RrdId} failed, retrying ({Attempt}/{Max})",
                    rrdId, transportFailures, ReportTransportRetries);
                await Task.Delay(_options.ReportRequestDelayMs, ct);
                continue;
            }
            catch (OperationCanceledException ex) when (!ct.IsCancellationRequested)
            {
                throw new TimeoutException(
                    $"Wildberries report page after rrdId {rrdId} did not arrive within {_options.ReportTimeoutSeconds} s.",
                    ex);
            }
            transportFailures = 0;

            if (rows.Count == 0)
                yield break;

            yield return rows;

            if (rows.Count < _options.ReportPageSize)
                yield break;
            rrdId = rows[^1].RrdId;
            await Task.Delay(_options.ReportRequestDelayMs, ct);
        }
    }

    // WB drops or stalls the connection mid-body on large pages, and answers 5xx now and then;
    // a 429 means another run took this minute's call, and the report pause outlasts it
    private static bool IsTransientReportFailure(Exception ex, CancellationToken ct) => ex switch
    {
        HttpRequestException { StatusCode: null } => true,
        TimeoutRejectedException => true,
        WildberriesApiException
        {
            StatusCode: >= 500 or StatusCodes.Status408RequestTimeout or StatusCodes.Status429TooManyRequests,
        } => true,
        OperationCanceledException => !ct.IsCancellationRequested,
        _ => false,
    };

    // «К клиенту при продаже», «К клиенту при отмене»; the way back reads «От клиента…» or «Возврат… (К продавцу)»
    private static bool IsToCustomer(string? bonusTypeName) =>
        bonusTypeName?.StartsWith("К клиенту", StringComparison.OrdinalIgnoreCase) == true;

    // «Возврат брака (К продавцу)», «Возврат брака (От продавца при отмене)», «Возврат товара продавцу по отзыву»
    private static bool IsToSeller(string? bonusTypeName) =>
        bonusTypeName?.Contains("продавц", StringComparison.OrdinalIgnoreCase) == true;

    private static ExternalAccrual MapAccrual(SalesReportsDetailedRes row, DateOnly day, CardIndex cards)
    {
        var currency = NullIfEmpty(row.Currency) ?? "RUB";
        var sku = cards.ByBarcode(row.Sku)?.ChrtId;
        var operation = NullIfEmpty(row.SellerOperName);
        var lines = new List<ExternalAccrualLine>();

        void Add(MarketplaceAccrualCategory category, decimal amount)
        {
            if (amount != 0)
                lines.Add(new ExternalAccrualLine(category, operation, sku, amount, currency));
        }

        var sign = row.DocTypeName == "Возврат" ? -1m : 1m;
        var retail = Money(row.RetailAmount);
        var forPay = Money(row.ForPay);
        var acquiring = Money(row.AcquiringFee);
        if (retail != 0)
        {
            Add(MarketplaceAccrualCategory.Sale, sign * retail);
            Add(MarketplaceAccrualCategory.Acquiring, -sign * acquiring);
            // forPay = retail - commission (vw, vwNds, ppvzReward) - acquiring
            Add(MarketplaceAccrualCategory.Commission, -sign * (retail - forPay - acquiring));
        }
        else if (forPay != 0)
        {
            Add(MarketplaceAccrualCategory.Other, sign * forPay);
        }

        var toClient = row.BonusTypeName is null || IsToCustomer(row.BonusTypeName);
        Add(toClient ? MarketplaceAccrualCategory.Logistics : MarketplaceAccrualCategory.ReturnLogistics,
            -Money(row.DeliveryService));
        Add(MarketplaceAccrualCategory.Logistics, -Money(row.RebillLogisticCost));
        Add(MarketplaceAccrualCategory.Storage, -Money(row.PaidStorage));
        Add(MarketplaceAccrualCategory.Penalty, -Money(row.Penalty));
        Add(row.BonusTypeName?.Contains("Продвижение", StringComparison.OrdinalIgnoreCase) == true
            ? MarketplaceAccrualCategory.Advertising
            : MarketplaceAccrualCategory.Other, -Money(row.Deduction));
        Add(MarketplaceAccrualCategory.Processing, -Money(row.PaidAcceptance));
        Add(MarketplaceAccrualCategory.Compensation, Money(row.AdditionalPayment));
        Add(MarketplaceAccrualCategory.Compensation, Money(row.CashbackDiscount));
        Add(MarketplaceAccrualCategory.Bonus, -Money(row.CashbackAmount));

        var id = row.RrdId.ToString(CultureInfo.InvariantCulture);
        var exemplar = row.ShkId > 0 ? row.ShkId.ToString(CultureInfo.InvariantCulture) : null;
        if (row.OrderId != 0)
            return new ExternalAccrual(id, day, MarketplaceAccrualScope.Posting,
                row.OrderId.ToString(CultureInfo.InvariantCulture), lines, ExemplarId: exemplar);

        // an FBS sale, its delivery and handling come with orderId 0 now and then; srid is still the task's rid
        return NullIfEmpty(row.OrderUid) is { } orderUid
            ? new ExternalAccrual(id, day, MarketplaceAccrualScope.Item, orderUid, lines, Rid: NullIfEmpty(row.Srid),
                ExemplarId: exemplar)
            : new ExternalAccrual(id, day, MarketplaceAccrualScope.Account, null, lines);
    }

    public async Task<DateTime?> GetEarliestPostingDateAsync(CancellationToken ct)
    {
        // the archive starts where live tasks end, so its oldest non-empty month holds the answer when there is one
        var month = MonthOf(DateTimeOffset.UtcNow);
        DateOnly? oldest = null;
        for (var i = 0; i < EarliestProbeMonths; i++, month = month.AddMonths(-1))
        {
            var page = await marketplace.GetV3FbsOrdersArchiveAsync(month.Year, month.Month, 0, ArchivePageSize, ct);
            if (page.Orders.Count > 0)
                oldest = month;
            await Task.Delay(_options.PageDelayMs, ct);
        }

        if (oldest is { } found)
        {
            DateTime? earliest = null;
            await foreach (var tasks in ListArchiveAsync(found, ct))
                foreach (var created in tasks.Select(t => t.CreatedAt).OfType<DateTime>())
                    earliest = earliest is null || created < earliest ? created : earliest;
            return earliest;
        }

        DateTime? live = null;
        var to = DateTimeOffset.UtcNow;
        for (var i = 0; i < EarliestProbeMonths; i++, to -= OrdersWindow)
        {
            DateTime? window = null;
            await foreach (var tasks in ListLiveTasksAsync(to - OrdersWindow, to, ct))
                foreach (var created in tasks.Select(t => t.CreatedAt).OfType<DateTime>())
                    window = window is null || created < window ? created : window;
            if (window is null && live is not null)
                break;
            live = window ?? live;
        }

        return live;
    }

    public async Task<IReadOnlyList<ExternalSupply>> GetSuppliesAsync(CancellationToken ct)
    {
        var result = new List<ExternalSupply>();
        long next = 0;
        while (true)
        {
            var page = await marketplace.GetV3SuppliesAsync(SuppliesPageSize, next, ct);
            var supplies = page.Supplies ?? [];
            foreach (var supply in supplies)
            {
                if (supply.Id is not { Length: > 0 } id || supply.CreatedAt is not { } createdAt)
                {
                    logger.LogWarning("Wildberries listed a supply without an id or a creation date: {SupplyId}",
                        supply.Id);
                    continue;
                }

                result.Add(new ExternalSupply(id, NullIfEmpty(supply.Name), createdAt.UtcDateTime,
                    (supply.ClosedAt ?? supply.ScanDt)?.UtcDateTime, supply.Done == true));
            }

            if (supplies.Count < SuppliesPageSize || page.Next is not { } cursor || cursor == 0)
                break;
            next = cursor;
            await Task.Delay(_options.PageDelayMs, ct);
        }

        return result;
    }

    public async IAsyncEnumerable<ExternalSupplyPostings> GetSupplyPostingsAsync(IReadOnlyList<string> supplyIds,
        [EnumeratorCancellation] CancellationToken ct)
    {
        for (var i = 0; i < supplyIds.Count; i++)
        {
            if (i > 0)
                await Task.Delay(_options.PageDelayMs, ct);

            SupplyOrderIDsAPI response;
            try
            {
                response = await marketplace.GetV3SuppliesSupplyIdOrderIdsAsync(supplyIds[i], ct);
            }
            catch (WildberriesApiException ex) when (ex.StatusCode == StatusCodes.Status404NotFound)
            {
                logger.LogWarning("Wildberries no longer knows supply {SupplyId}", supplyIds[i]);
                continue;
            }

            yield return new ExternalSupplyPostings(supplyIds[i], (response.OrderIds ?? [])
                .Select(id => id.ToString(CultureInfo.InvariantCulture))
                .ToList());
        }
    }

    public async Task<ExternalLabelDocument> GetStickersAsync(IReadOnlyList<string> postingNumbers, CancellationToken ct)
    {
        var ids = postingNumbers
            .Select(n => long.TryParse(n, NumberStyles.None, CultureInfo.InvariantCulture, out var id) ? id : (long?)null)
            .OfType<long>()
            .ToList();
        var response = await marketplace.PostV3OrdersStickersAsync(PostV3OrdersStickersType.Png,
            PostV3OrdersStickersWidth._58, PostV3OrdersStickersHeight._40,
            new PostV3OrdersStickersRequest { Orders = ids }, ct);

        // stickers come back in no particular order, and a task WB will not print is silently left out
        var pages = new List<ExternalLabelPage>();
        var unreadable = new List<ExternalLabelFailure>();
        foreach (var sticker in (response.Stickers ?? []).Where(s => s.OrderId is not null && !string.IsNullOrEmpty(s.File)))
        {
            var postingNumber = sticker.OrderId!.Value.ToString(CultureInfo.InvariantCulture);
            try
            {
                pages.Add(new ExternalLabelPage(postingNumber,
                    LabelPdfComposer.FromImage(Convert.FromBase64String(sticker.File!), StickerWidthMm,
                        StickerHeightMm, _options.StickerScale),
                    LabelKind.WildberriesSticker,
                    NullIfEmpty(sticker.PartA) is { } partA && NullIfEmpty(sticker.PartB) is { } partB
                        ? partA + partB
                        : null));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Wildberries sticker of task {PostingNumber} could not be read", postingNumber);
                unreadable.Add(new ExternalLabelFailure(postingNumber, ex.Message));
            }
        }

        return new ExternalLabelDocument(true, postingNumbers, "application/pdf", null, Pages: pages,
            Unreadable: unreadable);
    }

    private async IAsyncEnumerable<IReadOnlyList<AssemblyTask>> ListLiveTasksAsync(DateTimeOffset since,
        DateTimeOffset to, [EnumeratorCancellation] CancellationToken ct)
    {
        for (var windowStart = since; windowStart < to; windowStart += OrdersWindow)
        {
            var windowEnd = windowStart + OrdersWindow < to ? windowStart + OrdersWindow : to;
            long next = 0;
            while (true)
            {
                var page = await marketplace.GetV3OrdersAsync(OrdersPageSize, next,
                    windowStart.ToUnixTimeSeconds(), windowEnd.ToUnixTimeSeconds(), ct);
                var orders = page.Orders ?? [];
                var tasks = orders.Where(o => o.Id is not null).Select(FromLive).ToList();
                if (tasks.Count > 0)
                    yield return tasks;

                if (orders.Count < OrdersPageSize || page.Next is not { } cursor || cursor == 0)
                    break;
                next = cursor;
                await Task.Delay(_options.PageDelayMs, ct);
            }
        }
    }

    private async IAsyncEnumerable<IReadOnlyList<AssemblyTask>> ListArchiveAsync(DateOnly month,
        [EnumeratorCancellation] CancellationToken ct)
    {
        long next = 0;
        while (true)
        {
            var page = await marketplace.GetV3FbsOrdersArchiveAsync(month.Year, month.Month, next, ArchivePageSize, ct);
            if (page.Orders.Count > 0)
                yield return page.Orders.Select(FromArchive).ToList();

            if (page.Orders.Count < ArchivePageSize || page.Next is not { } cursor || cursor == 0)
                break;
            next = cursor;
            await Task.Delay(_options.PageDelayMs, ct);
        }
    }

    private async Task<Dictionary<long, (string? Supplier, string? Wb)>> GetRawStatusesAsync(
        IReadOnlyList<long> ids, CancellationToken ct)
    {
        var result = new Dictionary<long, (string?, string?)>(ids.Count);
        foreach (var batch in ids.Chunk(StatusBatchSize))
        {
            var response = await marketplace.PostV3OrdersStatusAsync(new PostV3OrdersStatusRequest { Orders = batch }, ct);
            // a task WB no longer knows is simply absent from the answer
            foreach (var status in response.Orders ?? [])
                if (status.Id is { } id)
                    result[id] = (status.SupplierStatus, status.WbStatus);
        }

        return result;
    }

    private async Task<ExternalPosting> MapPostingAsync(AssemblyTask task, SupplyDates supplies, CancellationToken ct)
    {
        var number = task.Id.ToString(CultureInfo.InvariantCulture);
        var deliveringAt = task.Supplier == "complete" && task.SupplyId is { Length: > 0 } supplyId
            ? await supplies.GetAsync(supplyId, ct)
            : null;
        var item = new ExternalPostingItem(
            task.ChrtId?.ToString(CultureInfo.InvariantCulture),
            task.Article ?? "",
            task.Article ?? "",
            1,
            CustomerPrice: task.Price / 100m,
            CustomerCurrencyCode: task.CurrencyCode is { } code ? Currencies.GetValueOrDefault(code) : null,
            Price: task.ConvertedPrice / 100m,
            CurrencyCode: task.ConvertedCurrencyCode is { } converted ? Currencies.GetValueOrDefault(converted) : "RUB");

        return new ExternalPosting(
            number,
            NullIfEmpty(task.OrderUid),
            MapStatus(number, task.Supplier, task.Wb),
            task.Supplier,
            task.Wb,
            task.WarehouseId?.ToString(CultureInfo.InvariantCulture),
            null,
            null,
            task.CreatedAt,
            NullIfEmpty(task.TrackNumber),
            1,
            MapCancellation(task.Supplier, task.Wb),
            [item],
            ExternalPostingScheme.Fbs,
            task.CreatedAt,
            // the archive's sticker id is the label's partA + partB
            LabelBarcode: task.StickerId?.ToString(CultureInfo.InvariantCulture),
            Dates: Dates(deliveringAt),
            SupplyExternalId: NullIfEmpty(task.SupplyId),
            Rid: NullIfEmpty(task.Rid));
    }

    private SupplyDates NewSupplyDates() => new(marketplace, _options.PageDelayMs);

    private static ExternalPostingDates? Dates(DateTime? deliveringAt) =>
        deliveringAt is null ? null : ExternalPostingDates.None with { DeliveringAt = deliveringAt };

    private static bool IsCancelled(string? supplier, string? wb) =>
        supplier == "cancel" || (wb is not null && CancelledWbStatuses.Contains(wb));

    private MarketplaceOrderStatus MapStatus(string postingNumber, string? supplier, string? wb)
    {
        if (IsCancelled(supplier, wb))
            return MarketplaceOrderStatus.Cancelled;
        if (wb == "sold")
            return MarketplaceOrderStatus.Delivered;

        switch (supplier)
        {
            case "new":
                return MarketplaceOrderStatus.AwaitingPackaging;
            case "confirm":
                return MarketplaceOrderStatus.AwaitingDeliver;
            case "complete":
                return MarketplaceOrderStatus.Delivering;
            default:
                logger.LogWarning("Wildberries task {PostingNumber} has an unknown status {SupplierStatus}/{WbStatus}",
                    postingNumber, supplier, wb);
                return MarketplaceOrderStatus.Unknown;
        }
    }

    private static ExternalCancellation? MapCancellation(string? supplier, string? wb)
    {
        if (!IsCancelled(supplier, wb))
            return null;

        return wb switch
        {
            "canceled_by_carrier" or "cancel_carrier" => new ExternalCancellation(true,
                MarketplaceCancellationType.Delivery, wb, wb, MarketplaceLossReason.DeliveryFailed, null),
            "defect" => new ExternalCancellation(true,
                MarketplaceCancellationType.Marketplace, wb, wb, MarketplaceLossReason.Defect, null),
            "canceled_by_client" or "declined_by_client" when supplier != "cancel" => new ExternalCancellation(
                supplier is "confirm" or "complete",
                MarketplaceCancellationType.Customer, wb, wb,
                wb == "declined_by_client" ? MarketplaceLossReason.BuyerChangedMind : null, null),
            _ => new ExternalCancellation(false,
                MarketplaceCancellationType.Seller, wb ?? supplier, wb ?? supplier, MarketplaceLossReason.SellerCancelled,
                null),
        };
    }

    private static AssemblyTask FromLive(Neisvestney.Marketplaces.Wildberries.Order o) => new(
        o.Id!.Value, o.OrderUid, o.Rid, o.WarehouseId, o.SupplyId, o.CreatedAt?.UtcDateTime, o.ChrtId, o.Article,
        o.Price, o.ConvertedPrice, o.CurrencyCode, o.ConvertedCurrencyCode, o.TrackNumber, null, null, null);

    private static AssemblyTask FromArchive(ArchiveOrder o) => new(
        o.Id, o.OrderUid, o.Rid, o.WarehouseId, o.SupplyId, ParseDate(o.CreatedAt), o.Product.ChrtId,
        o.Product.Article, o.PriceInfo.Price, o.PriceInfo.ConvertedPrice, o.PriceInfo.CurrencyCode,
        o.PriceInfo.ConvertedCurrencyCode, null, o.StickerId, o.Status.SupplierStatus, o.Status.WbStatus);

    private static IEnumerable<DateOnly> Months(DateTimeOffset since, DateTimeOffset to)
    {
        for (var month = MonthOf(since); month <= MonthOf(to); month = month.AddMonths(1))
            yield return month;
    }

    /// <summary>First day of the month the archive files a moment under: WB counts it by Moscow time.</summary>
    private static DateOnly MonthOf(DateTimeOffset moment)
    {
        var moscow = TimeZoneInfo.ConvertTimeBySystemTimeZoneId(moment, MoscowZoneId);
        return new DateOnly(moscow.Year, moscow.Month, 1);
    }

    private static DateOnly MoscowToday() =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTime.UtcNow, MoscowZoneId));

    private static DateTime? ParseDate(string? value) =>
        DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed)
            ? parsed.UtcDateTime
            : null;

    // the statistics report states Moscow wall time without an offset
    private static DateTime? ParseMoscowDate(string? value) =>
        DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
            ? TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(parsed, DateTimeKind.Unspecified), MoscowZone)
            : null;

    // the leading calendar day, whether or not a time follows it
    private static DateOnly? ParseDay(string? value) =>
        value is { Length: >= 10 }
        && DateOnly.TryParseExact(value[..10], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day)
            ? day
            : null;

    private static decimal Money(string? value) =>
        decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount) ? amount : 0;

    private static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

    /// <summary>A live or archived assembly task, cut down to what the mapping reads.</summary>
    private sealed record AssemblyTask(
        long Id,
        string? OrderUid,
        string? Rid,
        long? WarehouseId,
        string? SupplyId,
        DateTime? CreatedAt,
        ulong? ChrtId,
        string? Article,
        long? Price,
        long? ConvertedPrice,
        long? CurrencyCode,
        long? ConvertedCurrencyCode,
        string? TrackNumber,
        long? StickerId,
        string? Supplier,
        string? Wb);

    /// <summary>When each supply was handed over, read once per call from the supply list.</summary>
    private sealed class SupplyDates(IWildberriesMarketplaceClient marketplace, int pageDelayMs)
    {
        private Dictionary<string, DateTime?>? _closedAt;

        public async Task<DateTime?> GetAsync(string supplyId, CancellationToken ct)
        {
            if (_closedAt is null)
            {
                _closedAt = new Dictionary<string, DateTime?>(StringComparer.Ordinal);
                long next = 0;
                while (true)
                {
                    var page = await marketplace.GetV3SuppliesAsync(SuppliesPageSize, next, ct);
                    var supplies = page.Supplies ?? [];
                    foreach (var supply in supplies.Where(s => s.Id is not null))
                        _closedAt[supply.Id!] = (supply.ClosedAt ?? supply.ScanDt)?.UtcDateTime;

                    if (supplies.Count < SuppliesPageSize || page.Next is not { } cursor || cursor == 0)
                        break;
                    next = cursor;
                    await Task.Delay(pageDelayMs, ct);
                }
            }

            return _closedAt.GetValueOrDefault(supplyId);
        }
    }

    /// <summary>Barcode to size, for report rows that name a product only by its barcode.</summary>
    private sealed class CardIndex(Dictionary<string, (string ChrtId, string VendorCode)> byBarcode)
    {
        public static async Task<CardIndex> LoadAsync(WildberriesClient owner, CancellationToken ct)
        {
            var map = new Dictionary<string, (string, string)>(StringComparer.Ordinal);
            await foreach (var page in owner.GetCardsAsync(ct))
                foreach (var card in page)
                    foreach (var barcode in card.Barcodes)
                        map.TryAdd(barcode, (card.Sku!, card.OfferId));
            return new CardIndex(map);
        }

        public (string ChrtId, string VendorCode)? ByBarcode(string? barcode) =>
            barcode is { Length: > 0 } && byBarcode.TryGetValue(barcode, out var card) ? card : null;
    }
}
