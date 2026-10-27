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
    IOptions<MarketplacesOptions> options,
    ILogger<WildberriesClient> logger) : IWildberriesClient
{
    private const string MoscowZoneId = "Europe/Moscow";

    private const int CardsPageSize = 100;
    private const int OrdersPageSize = 1000;
    private const int StatusBatchSize = 1000;
    private const int ArchivePageSize = 1000;
    private const int SuppliesPageSize = 1000;
    private const int ReturnsPageSize = 1000;
    private const int ReportTransportRetries = 2;

    /// <summary>/api/v3/orders rejects a creation window longer than 30 days.</summary>
    private static readonly TimeSpan OrdersWindow = TimeSpan.FromDays(30) - TimeSpan.FromSeconds(1);

    /// <summary>How many archive months the earliest-posting probe walks back.</summary>
    private const int EarliestProbeMonths = 36;

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
        // assembly tasks are FBS only; the marketplace's own fulfilment has no task to import
        if (query.Scheme != ExternalPostingScheme.Fbs)
            yield break;

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

        CardIndex? cards = null;
        var tasksBySticker = new Dictionary<long, AssemblyTask>();
        var monthsRead = new HashSet<DateOnly>();

        for (var offset = 0L; ; offset += ReturnsPageSize)
        {
            var page = await analytics.GetV1GoodsReturnAsync(
                new DateTimeOffset(from.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero),
                new DateTimeOffset(today.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero),
                null, ReturnsPageSize, offset, ct);
            var rows = page.Report;
            if (rows.Count == 0)
                break;

            // a return reaches its task only through the sticker of the archived task, so the task's month is read
            var months = rows.Select(r => ParseDay(r.OrderDt)).OfType<DateOnly>()
                .Select(d => new DateOnly(d.Year, d.Month, 1)).Distinct();
            foreach (var month in months)
            {
                if (!monthsRead.Add(month))
                    continue;
                await foreach (var tasks in ListArchiveAsync(month, ct))
                    foreach (var task in tasks.Where(t => t.StickerId is not null))
                        tasksBySticker[task.StickerId!.Value] = task;
            }

            cards ??= await CardIndex.LoadAsync(this, ct);
            yield return rows.Select(r => MapReturn(r, tasksBySticker, cards)).ToList();

            if (rows.Count < ReturnsPageSize)
                break;
            await Task.Delay(_options.PageDelayMs, ct);
        }
    }

    private static ExternalReturn MapReturn(Report row, Dictionary<long, AssemblyTask> tasksBySticker, CardIndex cards)
    {
        var task = long.TryParse(row.StickerId, NumberStyles.None, CultureInfo.InvariantCulture, out var sticker)
            ? tasksBySticker.GetValueOrDefault(sticker)
            : null;
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
            // the task is already counted as cancelled, so the return is never subtracted from sales again
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
        var today = MoscowToday();
        var last = to > today ? today : to;
        var lookback = last.AddDays(-_options.AccrualsLookbackDays);
        var first = lookback < from ? lookback : from;
        if (first > last)
            yield break;

        CardIndex? cards = null;
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

            cards ??= await CardIndex.LoadAsync(this, ct);
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
            if (accruals.Count > 0)
                yield return accruals;

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

        var toClient = row.BonusTypeName?.StartsWith("К клиенту", StringComparison.OrdinalIgnoreCase) != false;
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

        var linked = row.OrderId != 0;
        return new ExternalAccrual(
            row.RrdId.ToString(CultureInfo.InvariantCulture),
            day,
            linked ? MarketplaceAccrualScope.Posting : MarketplaceAccrualScope.Account,
            linked ? row.OrderId.ToString(CultureInfo.InvariantCulture) : null,
            lines);
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
            Dates: Dates(deliveringAt));
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
        o.Id!.Value, o.OrderUid, o.WarehouseId, o.SupplyId, o.CreatedAt?.UtcDateTime, o.ChrtId, o.Article,
        o.Price, o.ConvertedPrice, o.CurrencyCode, o.ConvertedCurrencyCode, o.TrackNumber, null, null, null);

    private static AssemblyTask FromArchive(ArchiveOrder o) => new(
        o.Id, o.OrderUid, o.WarehouseId, o.SupplyId, ParseDate(o.CreatedAt), o.Product.ChrtId, o.Product.Article,
        o.PriceInfo.Price, o.PriceInfo.ConvertedPrice, o.PriceInfo.CurrencyCode, o.PriceInfo.ConvertedCurrencyCode,
        null, o.StickerId, o.Status.SupplierStatus, o.Status.WbStatus);

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
