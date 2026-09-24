using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using ProjectWarehouse.Server.Domain;
using ProjectWarehouse.Server.Infrastructure.Marketplaces;
using ProjectWarehouse.Server.Integrations.Abstractions;
using ProjectWarehouse.Server.Integrations.Ozon.Generated;

namespace ProjectWarehouse.Server.Integrations.Ozon;

public class OzonClient(
    IOzonApiClient api,
    IHttpClientFactory httpClients,
    IOptions<MarketplacesOptions> options,
    ILogger<OzonClient> logger) : IOzonClient
{
    /// <summary>Carries no Ozon credentials: the label file is served from a public CDN, not the API.</summary>
    public const string LabelDownloadClientName = "ozon-label-download";

    /// <summary>The only label task type seen so far; see <see cref="GetPackageLabelAsync"/>.</summary>
    private const string SmallLabel = "small_label";

    // spec caps: /v2/warehouse/list rejects limit > 200; /v3/product/list allows up to 1000
    private const int WarehousePageSize = 200;
    private const int CardPageSize = 200;
    private const int PostingPageSize = 100;

    /// <summary>Spec cap on <c>filter.order_numbers</c> of /v4/posting/fbs/list.</summary>
    private const int OrderNumberBatchSize = 100;

    /// <summary>
    /// Postings asked about by number in one call to /v3/posting/fbo/list. The spec allows 1000; a batch
    /// the size of a page keeps one call to one page.
    /// </summary>
    private const int PostingNumberBatchSize = PostingPageSize;

    /// <summary>The only posting state WMS assembles — see the FBS section of the marketplaces spec.</summary>
    private const string AwaitingDeliver = "awaiting_deliver";

    private const string AwaitingPackaging = "awaiting_packaging";

    private readonly OzonOptions _options = options.Value.Ozon;

    public async Task PingAsync(CancellationToken ct) =>
        await api.WarehouseListV2Async(new V2WarehouseListV2Request { Limit = 1 }, ct);

    public async Task<IReadOnlyList<ExternalWarehouse>> GetWarehousesAsync(CancellationToken ct)
    {
        var result = new List<ExternalWarehouse>();
        string? cursor = null;

        do
        {
            var response = await api.WarehouseListV2Async(
                new V2WarehouseListV2Request { Limit = WarehousePageSize, Cursor = cursor }, ct);

            foreach (var warehouse in response.Warehouses ?? [])
            {
                if (warehouse.Warehouse_id is not { } externalId)
                    continue;

                result.Add(new ExternalWarehouse(
                    externalId.ToString(CultureInfo.InvariantCulture),
                    warehouse.Name ?? externalId.ToString(CultureInfo.InvariantCulture),
                    ToKind(warehouse),
                    ToStatus(warehouse.Status),
                    warehouse.Status,
                    warehouse.Address_info?.Address));
            }

            cursor = response.Has_next == true ? response.Cursor : null;
            if (cursor is not null)
                await DelayBetweenPagesAsync(ct);
        } while (!string.IsNullOrEmpty(cursor));

        return result;
    }

    public async IAsyncEnumerable<IReadOnlyList<ExternalCard>> GetCardsAsync([EnumeratorCancellation] CancellationToken ct)
    {
        string? lastId = null;

        while (true)
        {
            var listResponse = await api.ProductAPI_GetProductListAsync(
                new Productv3GetProductListRequest { Limit = CardPageSize, Last_id = lastId, Filter = new Productv3GetProductListRequestFilter()}, ct);

            var productIds = (listResponse.Result?.Items ?? [])
                .Select(i => i.Product_id)
                .OfType<long>()
                .Select(id => id.ToString(CultureInfo.InvariantCulture))
                .ToList();

            if (productIds.Count == 0)
                yield break;

            // /v3/product/list returns identifiers only — the card itself needs a second call
            var infoResponse = await api.ProductAPI_GetProductInfoListAsync(
                new V3GetProductInfoListRequest { Product_id = productIds }, ct);

            var cards = (infoResponse.Items ?? [])
                .Select(ToExternalCard)
                .OfType<ExternalCard>()
                .ToList();

            if (cards.Count > 0)
                yield return cards;

            lastId = listResponse.Result?.Last_id;
            if (string.IsNullOrEmpty(lastId) || productIds.Count < CardPageSize)
                yield break;

            await DelayBetweenPagesAsync(ct);
        }
    }

    public async Task<ExternalSellerInfo> GetSellerInfoAsync(CancellationToken ct)
    {
        var company = (await api.SellerAPI_SellerInfoAsync(ct)).Company;

        return new ExternalSellerInfo(
            Trim(company?.Name),
            Trim(company?.Legal_name),
            Trim(company?.Inn),
            Trim(company?.Ogrn),
            Trim(company?.Ownership_form));
    }

    public async IAsyncEnumerable<IReadOnlyList<ExternalPosting>> GetActivePostingsAsync(
        [EnumeratorCancellation] CancellationToken ct)
    {
        string? cursor = null;

        // Ozon demands a cutoff (or delivering_date) window and answers 400 without one, even though its
        // spec marks no filter field as required. Cutoff is the assembly deadline, so the window is kept
        // wide on purpose — see OzonOptions.CutoffWindowPastDays.
        var now = DateTimeOffset.UtcNow;
        var cutoffFrom = now.AddDays(-_options.CutoffWindowPastDays);
        var cutoffTo = now.AddDays(_options.CutoffWindowFutureDays);

        do
        {
            var response = await api.PostingFbsUnfulfilledListAsync(
                new PostingFbsUnfulfilledListRequest
                {
                    Limit = PostingPageSize,
                    Cursor = cursor,
                    // oldest first, so pages stay stable while new postings keep arriving
                    Sort_dir = PostingFbsUnfulfilledListRequestSortDirEnum.ASC,
                    Filter = new PostingFbsUnfulfilledListRequestFilter
                    {
                        Statuses = [AwaitingDeliver],
                        Cutoff_from = cutoffFrom,
                        Cutoff_to = cutoffTo,
                    },
                    // rides along on a call already being made — no extra request, no extra rate limit
                    With = new PostingFbsUnfulfilledListRequestWith { Financial_data = true },
                }, ct);

            var postings = (response.Postings ?? [])
                .Select(ToExternalPosting)
                .OfType<ExternalPosting>()
                .ToList();

            if (postings.Count > 0)
                yield return postings;

            cursor = response.Has_next == true ? response.Cursor : null;
            if (!string.IsNullOrEmpty(cursor))
                await DelayBetweenPagesAsync(ct);
        } while (!string.IsNullOrEmpty(cursor));
    }

    public async IAsyncEnumerable<IReadOnlyList<ExternalPosting>> GetPostingsAsync(
        ExternalPostingQuery query, [EnumeratorCancellation] CancellationToken ct)
    {
        var statuses = ToRawStatuses(query.Scheme, query.Statuses);
        var firstCall = true;

        foreach (var (since, to) in PeriodWindows(query.Since, query.To))
        {
            string? cursor = null;

            do
            {
                if (!firstCall)
                    await DelayBetweenPagesAsync(ct);
                firstCall = false;

                var page = await FetchPostingPageAsync(query.Scheme, since, to, statuses, null, cursor, ct);

                if (page.Postings.Count > 0)
                    yield return page.Postings;

                cursor = page.Cursor;
            } while (!string.IsNullOrEmpty(cursor));
        }
    }

    public async Task<DateTime?> GetEarliestPostingDateAsync(CancellationToken ct)
    {
        var fbs = await ProbeEarliestPostingDateAsync(ExternalPostingScheme.Fbs, ct);
        var fbo = await ProbeEarliestPostingDateAsync(ExternalPostingScheme.Fbo, ct);

        if (fbs is null) return fbo;
        if (fbo is null) return fbs;
        return fbs < fbo ? fbs : fbo;
    }

    /// <summary>
    /// Walks whole windows backwards asking for the single oldest posting in each, and stops at the first
    /// one that is empty — a seller with no postings in a whole year had none before it either.
    /// </summary>
    private async Task<DateTime?> ProbeEarliestPostingDateAsync(ExternalPostingScheme scheme, CancellationToken ct)
    {
        var window = TimeSpan.FromDays(_options.PostingPeriodWindowDays);
        var to = DateTimeOffset.UtcNow;
        DateTime? earliest = null;

        for (var step = 0; step < _options.EarliestPostingProbeWindows; step++)
        {
            if (step > 0)
                await DelayBetweenPagesAsync(ct);

            var since = to - window;
            var page = await FetchPostingPageAsync(scheme, since, to, null, null, null, ct, limit: 1);

            if (page.Postings.Count == 0)
                break;

            var posting = page.Postings[0];
            // FBS postings carry no creation date of their own; in_process_at is the closest thing to one
            earliest = posting.CreatedAt ?? posting.InProcessAt ?? since.UtcDateTime;
            to = since;
        }

        return earliest;
    }

    private async Task<(List<ExternalPosting> Postings, string? Cursor)> FetchPostingPageAsync(
        ExternalPostingScheme scheme, DateTimeOffset since, DateTimeOffset to,
        IReadOnlyList<string>? statuses, IReadOnlyList<string>? postingNumbers, string? cursor,
        CancellationToken ct, int limit = PostingPageSize)
    {
        if (scheme == ExternalPostingScheme.Fbo)
        {
            var fbo = await api.PostingFboListAsync(
                new PostingFboListRequest
                {
                    Limit = limit,
                    Cursor = cursor,
                    // oldest first, so pages stay stable while new postings keep arriving
                    Sort_dir = PostingFboListRequestSortDirEnum.ASC,
                    Filter = new PostingFboListRequestFilter
                    {
                        Since = since,
                        To = to,
                        Statuses = statuses,
                        Posting_numbers = postingNumbers,
                    },
                    With = new PostingFboListRequestWith { Financial_data = true },
                }, ct);

            return ((fbo.Postings ?? []).Select(ToExternalPosting).OfType<ExternalPosting>().ToList(),
                fbo.Has_next == true ? fbo.Cursor : null);
        }

        var fbs = await api.PostingFbsListAsync(
            new PostingFbsListRequest
            {
                Limit = limit,
                Cursor = cursor,
                Sort_dir = PostingFbsListRequestSortDirEnum.ASC,
                Filter = new PostingFbsListRequestFilter
                {
                    Since = since,
                    To = to,
                    Statuses = statuses,
                },
                With = new PostingFbsListRequestWith { Financial_data = true },
            }, ct);

        return ((fbs.Postings ?? []).Select(ToExternalPosting).OfType<ExternalPosting>().ToList(),
            fbs.Has_next == true ? fbs.Cursor : null);
    }

    /// <summary>Ozon answers <c>PERIOD_IS_TOO_LONG</c> past a year, so a longer period is asked for in slices.</summary>
    private IEnumerable<(DateTimeOffset Since, DateTimeOffset To)> PeriodWindows(DateTime since, DateTime to)
    {
        var window = TimeSpan.FromDays(_options.PostingPeriodWindowDays);
        var cursor = new DateTimeOffset(DateTime.SpecifyKind(since, DateTimeKind.Utc));
        var end = new DateTimeOffset(DateTime.SpecifyKind(to, DateTimeKind.Utc));

        while (cursor < end)
        {
            var next = cursor + window;
            if (next > end)
                next = end;

            yield return (cursor, next);
            cursor = next;
        }
    }

    /// <summary>
    /// WMS statuses expanded back into Ozon's own vocabulary. Null asks for every status — the two lists
    /// answer with all of them when <c>filter.statuses</c> is omitted.
    /// </summary>
    private static IReadOnlyList<string>? ToRawStatuses(
        ExternalPostingScheme scheme, IReadOnlyList<MarketplaceOrderStatus>? statuses)
    {
        if (statuses is null || statuses.Count == 0)
            return null;

        var names = statuses.SelectMany(s => RawStatusesOf(scheme, s)).Distinct().ToList();
        return names.Count > 0 ? names : null;
    }

    /// <summary>
    /// What may be <b>asked for</b>, which is not the same set as what may be <b>answered</b> — see
    /// <see cref="ToOrderStatus"/>. FBO's vocabulary is a subset of FBS's: no carrier handover, no
    /// arbitration, no refusal on acceptance.
    /// </summary>
    private static string[] RawStatusesOf(ExternalPostingScheme scheme, MarketplaceOrderStatus status) =>
        (scheme, status) switch
        {
            (_, MarketplaceOrderStatus.AwaitingDeliver) => [AwaitingPackaging, AwaitingDeliver],
            (ExternalPostingScheme.Fbo, MarketplaceOrderStatus.Delivering) => ["delivering"],
            // sent_by_seller is documented as a filter value and rejected as one by the live API, which
            // answers 400 listing what it really takes. It stays in ToOrderStatus: an answer may carry it.
            (_, MarketplaceOrderStatus.Delivering) => ["delivering", "driver_pickup"],
            (_, MarketplaceOrderStatus.Delivered) => ["delivered"],
            (ExternalPostingScheme.Fbo, MarketplaceOrderStatus.Cancelled) => ["cancelled"],
            (_, MarketplaceOrderStatus.Cancelled) => ["cancelled", "not_accepted"],
            (ExternalPostingScheme.Fbo, MarketplaceOrderStatus.Arbitration) => [],
            (_, MarketplaceOrderStatus.Arbitration) => ["arbitration", "client_arbitration"],
            _ => [],
        };

    public async Task<IReadOnlyList<ExternalPostingStatus>> GetPostingStatusesAsync(
        IReadOnlyList<string> postingNumbers, ExternalPostingScheme scheme, CancellationToken ct)
    {
        return scheme == ExternalPostingScheme.Fbo
            ? await GetFboPostingStatusesAsync(postingNumbers, ct)
            : await GetFbsPostingStatusesAsync(postingNumbers, ct);
    }

    /// <summary>
    /// Unlike its FBS counterpart, <c>/v3/posting/fbo/list</c> filters by posting number directly, so the
    /// detour through order numbers — and the unrelated postings it drags in — is not needed here.
    /// </summary>
    private async Task<IReadOnlyList<ExternalPostingStatus>> GetFboPostingStatusesAsync(
        IReadOnlyList<string> postingNumbers, CancellationToken ct)
    {
        var wanted = postingNumbers.Distinct().ToList();
        var statuses = new List<ExternalPostingStatus>(wanted.Count);

        var now = DateTimeOffset.UtcNow;
        var since = now.AddDays(-_options.PostingWindowPastDays);
        var to = now.AddDays(_options.PostingWindowFutureDays);

        var firstCall = true;

        foreach (var batch in wanted.Chunk(PostingNumberBatchSize))
        {
            string? cursor = null;

            do
            {
                if (!firstCall)
                    await DelayBetweenPagesAsync(ct);
                firstCall = false;

                var page = await FetchPostingPageAsync(
                    ExternalPostingScheme.Fbo, since, to, null, batch, cursor, ct);

                statuses.AddRange(page.Postings.Select(ToPostingStatus));
                cursor = page.Cursor;
            } while (!string.IsNullOrEmpty(cursor));
        }

        return statuses;
    }

    private static ExternalPostingStatus ToPostingStatus(ExternalPosting posting) =>
        new(posting.PostingNumber, posting.Status, posting.RawStatus, posting.RawSubstatus,
            posting.TrackingNumber, posting.Cancellation, posting.Items);

    private async Task<IReadOnlyList<ExternalPostingStatus>> GetFbsPostingStatusesAsync(
        IReadOnlyList<string> postingNumbers, CancellationToken ct)
    {
        var wanted = postingNumbers.ToHashSet();
        var statuses = new List<ExternalPostingStatus>(wanted.Count);

        var now = DateTimeOffset.UtcNow;
        var since = now.AddDays(-_options.PostingWindowPastDays);
        var to = now.AddDays(_options.PostingWindowFutureDays);

        var firstBatch = true;

        foreach (var batch in wanted.Select(ToOrderNumber).Distinct().Chunk(OrderNumberBatchSize))
        {
            // the page delay guards pages inside a batch; batches need it just as much
            if (!firstBatch)
                await DelayBetweenPagesAsync(ct);
            firstBatch = false;

            string? cursor = null;
            var matched = 0;

            do
            {
                var response = await api.PostingFbsListAsync(
                    new PostingFbsListRequest
                    {
                        Limit = PostingPageSize,
                        Cursor = cursor,
                        Filter = new PostingFbsListRequestFilter
                        {
                            Order_numbers = batch,
                            Since = since,
                            To = to,
                        },
                        With = new PostingFbsListRequestWith { Financial_data = true },
                    }, ct);

                foreach (var posting in response.Postings ?? [])
                {
                    // an order number pulls in all of its postings, including ones nobody asked about
                    if (posting.Posting_number is not { Length: > 0 } number || !wanted.Contains(number))
                        continue;

                    var financials = IndexFinancials(posting.Financial_data?.Products, p => p.Product_id);

                    statuses.Add(new ExternalPostingStatus(
                        number,
                        ToOrderStatus(posting.Status),
                        posting.Status,
                        posting.Substatus,
                        posting.Tracking_number,
                        ToCancellation(posting.Cancellation),
                        (posting.Products ?? [])
                            .Select(p => ToExternalPostingItem(p, financials))
                            .ToList()));
                    matched++;
                }

                cursor = response.Has_next == true ? response.Cursor : null;
                if (!string.IsNullOrEmpty(cursor))
                    await DelayBetweenPagesAsync(ct);
            } while (!string.IsNullOrEmpty(cursor));

            // a whole batch matching nothing reads as a broken filter, not as N forgotten postings
            if (matched == 0)
                logger.LogWarning("Ozon returned no postings for {Count} order number(s), first {OrderNumber}",
                    batch.Length, batch[0]);
        }

        return statuses;
    }

    /// <summary>
    /// <c>/v4/posting/fbs/list</c> filters by order, not by posting, and a posting number is its order
    /// number plus an index — <c>12345678-0012-1</c> belongs to <c>12345678-0012</c>.
    /// </summary>
    private static string ToOrderNumber(string postingNumber) =>
        postingNumber.LastIndexOf('-') is > 0 and var dash
            ? postingNumber[..dash]
            : postingNumber;

    /// <summary>
    /// Two calls: /v3/…/package-label/create raises an asynchronous task, /v2/…/package-label/get reports
    /// its progress and finally hands over a link to the file.
    /// </summary>
    /// <remarks>
    /// Creating a task twice for the same posting is idempotent — Ozon answers with the task it already
    /// has — so an unfinished task needs no bookkeeping here: the next call picks it back up.
    /// </remarks>
    public async Task<ExternalLabelDocument> GetPackageLabelAsync(
        IReadOnlyList<string> postingNumbers, CancellationToken ct)
    {
        var created = await api.PostingFbsPackageLabelCreateAsync(
            new PostingFbsPackageLabelCreateRequest { Posting_numbers = [.. postingNumbers] }, ct);

        var tasks = created.Tasks ?? [];
        if (tasks.Count == 0 || tasks[0].Task_id is not { } taskId)
        {
            logger.LogInformation("Ozon raised no label task for {Count} posting(s)", postingNumbers.Count);
            return NotReady();
        }

        // Only small_label has been seen in the wild. Anything else may lay the label out differently,
        // which would move the article overlay and break the scanit read — so it is worth hearing about.
        foreach (var task in tasks.Where(t => t.Task_type != SmallLabel))
            logger.LogWarning("Ozon raised a {TaskType} label task ({TaskId}) for {Count} posting(s)",
                task.Task_type, task.Task_id, postingNumbers.Count);

        if (tasks.Count > 1)
            logger.LogWarning("Ozon raised {TaskCount} label tasks for {Count} posting(s); using {TaskId}",
                tasks.Count, postingNumbers.Count, taskId);

        var attempts = Math.Max(1, _options.LabelPollAttempts);
        var pollDelay = Math.Max(0, _options.LabelPollDelayMs);

        for (var attempt = 1; attempt <= attempts; attempt++)
        {
            var state = await api.PostingFbsPackageLabelGetAsync(
                new PostingFbsPackageLabelGetRequest { Task_id = taskId }, ct);

            foreach (var unprinted in state.Status?.Unprinted_postings ?? [])
                logger.LogWarning("Ozon could not produce a label for {PostingNumber}: {Message}",
                    unprinted.Posting_number, unprinted.Message);

            switch (state.Status?.Code)
            {
                case "completed":
                    return await DownloadAsync(state, postingNumbers, ct);

                case "error":
                    throw new MarketplaceApiException(
                        "Ozon failed to produce the labels.", null,
                        $"{state.Error?.Code}: {state.Error?.Message}");
            }

            if (attempt < attempts)
                await Task.Delay(pollDelay, ct);
        }

        return NotReady();

        ExternalLabelDocument NotReady()
        {
            logger.LogInformation("Ozon has not produced labels for {Count} posting(s) yet", postingNumbers.Count);
            return new ExternalLabelDocument(false, postingNumbers, null, null);
        }
    }

    /// <summary>
    /// The file sits on a public CDN under a temporary path, so it is fetched without credentials and
    /// right away. Its Content-Type comes back as <c>application/octet-stream</c> and is not believed.
    /// </summary>
    private async Task<ExternalLabelDocument> DownloadAsync(
        PostingFbsPackageLabelGetResponse state, IReadOnlyList<string> postingNumbers, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(state.File_url))
            throw new MarketplaceApiException(
                "Ozon reported the labels as ready without a file link.", null, null);

        using var http = httpClients.CreateClient(LabelDownloadClientName);
        using var response = await http.GetAsync(state.File_url, ct);

        if (!response.IsSuccessStatusCode)
            throw new MarketplaceApiException(
                "Ozon's label file could not be downloaded.", (int)response.StatusCode,
                Truncate(await response.Content.ReadAsStringAsync(ct)));

        var bytes = await response.Content.ReadAsByteArrayAsync(ct);
        if (bytes.Length < 4 || bytes[0] != '%' || bytes[1] != 'P' || bytes[2] != 'D' || bytes[3] != 'F')
            throw new MarketplaceApiException(
                "Ozon's label file is not a PDF.", null, Truncate(Encoding.UTF8.GetString(bytes)));

        logger.LogInformation(
            "Ozon produced labels for {Printed}/{Requested} posting(s)",
            state.Status?.Printed_postings_count, state.Status?.Postings_count);

        var unprinted = (state.Status?.Unprinted_postings ?? [])
            .Where(u => !string.IsNullOrWhiteSpace(u.Posting_number))
            .Select(u => new ExternalLabelFailure(u.Posting_number!, u.Message))
            .ToList();

        return new ExternalLabelDocument(true, postingNumbers, "application/pdf", bytes, unprinted);
    }

    private static string Truncate(string text) => text[..Math.Min(text.Length, 2000)];

    private ExternalPosting? ToExternalPosting(PostingFbsUnfulfilledListResponsePostings posting)
    {
        if (string.IsNullOrWhiteSpace(posting.Posting_number))
            return null;

        var financials = IndexFinancials(posting.Financial_data?.Products, p => p.Product_id);

        return new ExternalPosting(
            posting.Posting_number,
            posting.Order_number,
            ToOrderStatus(posting.Status),
            posting.Status,
            posting.Substatus,
            posting.Delivery_method?.Warehouse_id?.ToString(CultureInfo.InvariantCulture),
            posting.Delivery_method?.Name,
            // the generated type is DateTimeOffset; the DbContext converter only fixes Unspecified kinds
            posting.Shipment_date?.UtcDateTime,
            posting.In_process_at?.UtcDateTime,
            posting.Tracking_number,
            posting.Multi_box_qty is > 0 ? posting.Multi_box_qty.Value : 1,
            ToCancellation(posting.Cancellation),
            (posting.Products ?? [])
                .Select(p => ToExternalPostingItem(p, financials))
                .ToList(),
            Scanit: NullIfBlank(posting.Scanit));
    }

    /// <summary>Ozon writes an absent scanit as an empty string, which is a value the domain has no use for.</summary>
    private static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

    /// <summary>
    /// An FBO posting has no seller warehouse, no carrier and no packages of its own: Ozon ships it from
    /// its own stock, and <c>analytics_data.warehouse_id</c> names an Ozon warehouse that maps to nothing here.
    /// </summary>
    private ExternalPosting? ToExternalPosting(PostingFboListResponsePostings posting)
    {
        if (string.IsNullOrWhiteSpace(posting.Posting_number))
            return null;

        var financials = IndexFinancials(posting.Financial_data?.Products, p => p.Product_id);

        return new ExternalPosting(
            posting.Posting_number,
            posting.Order_number,
            ToOrderStatus(posting.Status),
            posting.Status,
            posting.Substatus,
            WarehouseExternalId: null,
            DeliveryMethodName: null,
            ShipmentDate: null,
            posting.In_process_at?.UtcDateTime,
            TrackingNumber: null,
            MultiBoxQty: 1,
            ToCancellation(posting.Cancellation),
            (posting.Products ?? [])
                .Select(p => ToExternalPostingItem(p, financials))
                .ToList(),
            ExternalPostingScheme.Fbo,
            posting.Created_at?.UtcDateTime);
    }

    /// <summary>
    /// The history list, as opposed to the unfulfilled one: same payload, unrelated generated types, and
    /// no creation date — <c>filter.since</c>/<c>to</c> select on it but the response never states it.
    /// </summary>
    private ExternalPosting? ToExternalPosting(PostingFbsListResponsePostings posting)
    {
        if (string.IsNullOrWhiteSpace(posting.Posting_number))
            return null;

        var financials = IndexFinancials(posting.Financial_data?.Products, p => p.Product_id);

        return new ExternalPosting(
            posting.Posting_number,
            posting.Order_number,
            ToOrderStatus(posting.Status),
            posting.Status,
            posting.Substatus,
            posting.Delivery_method?.Warehouse_id?.ToString(CultureInfo.InvariantCulture),
            posting.Delivery_method?.Name,
            posting.Shipment_date?.UtcDateTime,
            posting.In_process_at?.UtcDateTime,
            posting.Tracking_number,
            posting.Multi_box_qty is > 0 ? posting.Multi_box_qty.Value : 1,
            ToCancellation(posting.Cancellation),
            (posting.Products ?? [])
                .Select(p => ToExternalPostingItem(p, financials))
                .ToList(),
            Scanit: NullIfBlank(posting.Scanit));
    }

    // financial_data indexes products by product_id, which is the same number products[] calls sku
    private static Dictionary<long, T> IndexFinancials<T>(IEnumerable<T>? products, Func<T, long?> productId) =>
        (products ?? [])
            .Where(p => productId(p) is not null)
            .GroupBy(p => productId(p)!.Value)
            .ToDictionary(g => g.Key, g => g.First());

    // the two posting endpoints carry the same product and financial payloads under unrelated generated types
    private static ExternalPostingItem ToExternalPostingItem(
        PostingFbsUnfulfilledListResponsePostingsProducts product,
        IReadOnlyDictionary<long, PostingFbsUnfulfilledListResponsePostingsFinancialDataProducts> financials)
    {
        var f = product.Sku is { } sku && financials.TryGetValue(sku, out var found) ? found : null;

        return ToExternalPostingItem(
            sku: product.Sku,
            offerId: product.Offer_id,
            name: product.Name,
            quantity: product.Quantity,
            priceCurrency: product.Price?.Currency,
            customerPrice: f?.Customer_price,
            price: f?.Price,
            oldPrice: f?.Old_price,
            discountValue: f?.Total_discount_value,
            payout: f?.Payout,
            commissionAmount: f?.Commission?.Amount,
            commissionCurrency: f?.Commission?.Currency);
    }

    private static ExternalPostingItem ToExternalPostingItem(
        PostingFbsListResponsePostingsProducts product,
        IReadOnlyDictionary<long, PostingFbsListResponsePostingsFinancialDataProducts> financials)
    {
        var f = product.Sku is { } sku && financials.TryGetValue(sku, out var found) ? found : null;

        return ToExternalPostingItem(
            sku: product.Sku,
            offerId: product.Offer_id,
            name: product.Name,
            quantity: product.Quantity,
            priceCurrency: product.Price?.Currency,
            customerPrice: f?.Customer_price,
            price: f?.Price,
            oldPrice: f?.Old_price,
            discountValue: f?.Total_discount_value,
            payout: f?.Payout,
            commissionAmount: f?.Commission?.Amount,
            commissionCurrency: f?.Commission?.Currency);
    }

    private static ExternalPostingItem ToExternalPostingItem(
        PostingFboListResponsePostingsProducts product,
        IReadOnlyDictionary<long, PostingFboListResponsePostingsFinancialDataProducts> financials)
    {
        var f = product.Sku is { } sku && financials.TryGetValue(sku, out var found) ? found : null;

        return ToExternalPostingItem(
            sku: product.Sku,
            offerId: product.Offer_id,
            name: product.Name,
            quantity: (int?)product.Quantity,
            priceCurrency: product.Price?.Currency,
            // FBO financial_data states no customer_price; the line's own price is what the buyer paid
            customerPrice: product.Price,
            price: f?.Price,
            oldPrice: f?.Old_price,
            discountValue: f?.Total_discount_value,
            payout: f?.Payout,
            commissionAmount: f?.Commission?.Amount,
            commissionCurrency: f?.Commission?.Currency);
    }

    private static ExternalPostingItem ToExternalPostingItem(long? sku, string? offerId, string? name,
        int? quantity, string? priceCurrency, PostingMoney? customerPrice, double? price, double? oldPrice,
        double? discountValue, double? payout, double? commissionAmount, string? commissionCurrency) =>
        new(
            sku?.ToString(CultureInfo.InvariantCulture),
            offerId ?? "",
            name ?? "",
            quantity ?? 0,
            ParsePrice(customerPrice?.Amount),
            Trim(customerPrice?.Currency),
            ToMoney(price),
            ToMoney(oldPrice),
            ToMoney(discountValue),
            ToMoney(payout),
            // financial_data states no currency of its own for these; the line's own price carries it
            Trim(priceCurrency),
            ToMoney(commissionAmount),
            Trim(commissionCurrency));

    /// <summary>Ozon types most financial amounts as <c>double</c>; money is kept as decimal in WMS.</summary>
    private static decimal? ToMoney(double? value) =>
        value is { } amount && double.IsFinite(amount) ? Math.Round((decimal)amount, 2) : null;

    /// <summary>Ozon posting states collapsed to the WMS vocabulary. Unknown values are logged, not guessed.</summary>
    private MarketplaceOrderStatus ToOrderStatus(string? status)
    {
        switch (status)
        {
            // FBO packs at Ozon's own warehouse, so awaiting_packaging is the same "not moving yet" state
            case AwaitingDeliver or AwaitingPackaging:
                return MarketplaceOrderStatus.AwaitingDeliver;
            case "delivering" or "driver_pickup" or "sent_by_seller":
                return MarketplaceOrderStatus.Delivering;
            case "delivered":
                return MarketplaceOrderStatus.Delivered;
            case "cancelled" or "not_accepted":
                return MarketplaceOrderStatus.Cancelled;
            case "arbitration" or "client_arbitration":
                return MarketplaceOrderStatus.Arbitration;
            default:
                logger.LogWarning(
                    "Ozon returned an unknown posting status {OzonPostingStatus}", status ?? "<null>");
                return MarketplaceOrderStatus.Unknown;
        }
    }

    // the two posting endpoints carry the same cancellation payload under two unrelated generated types
    private ExternalCancellation? ToCancellation(PostingFbsListResponsePostingsCancellation? cancellation) =>
        ToCancellation(cancellation?.Cancelled_after_ship, cancellation?.Cancellation_type,
            cancellation?.Cancel_reason);

    /// <summary>FBO states no <c>cancelled_after_ship</c> — there is no seller shipment to be after.</summary>
    private ExternalCancellation? ToCancellation(PostingFboListResponsePostingsCancellation? cancellation) =>
        ToCancellation(null, cancellation?.Cancellation_type, cancellation?.Cancel_reason);

    private ExternalCancellation? ToCancellation(
        PostingFbsUnfulfilledListResponsePostingsCancellation? cancellation) =>
        ToCancellation(cancellation?.Cancelled_after_ship, cancellation?.Cancellation_type,
            cancellation?.Cancel_reason);

    /// <summary>
    /// Ozon answers a live posting with an empty cancellation object rather than none — FBS with
    /// <c>cancelled_after_ship: false</c> in it, so that flag alone does not make a cancellation.
    /// </summary>
    private ExternalCancellation? ToCancellation(bool? afterShip, string? type, string? reason)
    {
        var rawType = Trim(type);
        var trimmedReason = Trim(reason);

        if (afterShip is null or false && rawType is null && trimmedReason is null)
            return null;

        return new ExternalCancellation(afterShip, ToCancellationType(rawType), rawType, trimmedReason);
    }

    /// <summary>
    /// Ozon cancellation initiators collapsed to the WMS vocabulary. FBS spells them in lower case, FBO
    /// capitalised.
    /// </summary>
    private MarketplaceCancellationType ToCancellationType(string? type)
    {
        switch (type?.ToLowerInvariant())
        {
            case null:
                return MarketplaceCancellationType.Unknown;
            case "seller":
                return MarketplaceCancellationType.Seller;
            case "client" or "customer":
                return MarketplaceCancellationType.Customer;
            case "ozon":
                return MarketplaceCancellationType.Marketplace;
            case "system":
                return MarketplaceCancellationType.System;
            case "delivery":
                return MarketplaceCancellationType.Delivery;
            default:
                logger.LogWarning("Ozon returned an unknown cancellation type {OzonCancellationType}", type);
                return MarketplaceCancellationType.Unknown;
        }
    }

    private static string? Trim(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private Task DelayBetweenPagesAsync(CancellationToken ct) =>
        _options.PageDelayMs > 0 ? Task.Delay(_options.PageDelayMs, ct) : Task.CompletedTask;

    private static MarketplaceWarehouseKind ToKind(WarehouseListV2ResponseWarehouse warehouse) =>
        warehouse switch
        {
            { Is_express: true } => MarketplaceWarehouseKind.Express,
            { Is_rfbs: true } => MarketplaceWarehouseKind.Rfbs,
            _ => MarketplaceWarehouseKind.Fbs,
        };

    /// <summary>Ozon warehouse states as shown in the seller cabinet. Anything else is treated as unusable.</summary>
    private MarketplaceWarehouseStatus ToStatus(string? status)
    {
        switch (status)
        {
            case "created":
                return MarketplaceWarehouseStatus.Active;
            case "disabled":
                return MarketplaceWarehouseStatus.Inactive;
            // known-unusable states are expected; anything else means Ozon extended the vocabulary
            case "new" or "disabled_due_to_limit" or "blocked" or "error":
                return MarketplaceWarehouseStatus.Unavailable;
            default:
                logger.LogWarning(
                    "Ozon returned an unknown warehouse status {OzonWarehouseStatus}, treating it as unavailable",
                    status ?? "<null>");
                return MarketplaceWarehouseStatus.Unavailable;
        }
    }

    private static ExternalCard? ToExternalCard(V3GetProductInfoListResponseItem item)
    {
        if (item.Id is not { } productId)
            return null;

        return new ExternalCard(
            productId.ToString(CultureInfo.InvariantCulture),
            item.Sku?.ToString(CultureInfo.InvariantCulture),
            item.Offer_id ?? "",
            item.Name ?? "",
            item.Barcodes?.Where(b => !string.IsNullOrWhiteSpace(b)).ToList() ?? [],
            item.Primary_image?.FirstOrDefault() ?? item.Images?.FirstOrDefault(),
            ParsePrice(item.Price),
            item.Currency_code,
            item.Is_archived == true || item.Is_autoarchived == true);
    }

    private static decimal? ParsePrice(string? price) =>
        decimal.TryParse(price, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed) ? parsed : null;
}
