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
    IOptions<MarketplacesOptions> options,
    ILogger<OzonClient> logger) : IOzonClient
{
    // spec caps: /v2/warehouse/list rejects limit > 200; /v3/product/list allows up to 1000
    private const int WarehousePageSize = 200;
    private const int CardPageSize = 200;
    private const int PostingPageSize = 100;

    /// <summary>The only posting state WMS imports — see the FBS section of the marketplaces spec.</summary>
    private const string AwaitingDeliver = "awaiting_deliver";

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

    public async Task<ExternalPostingStatus?> GetPostingStatusAsync(string postingNumber, CancellationToken ct)
    {
        V3FbsPostingDetail? posting;
        try
        {
            posting = (await api.PostingAPI_GetFbsPostingV3Async(
                new Postingv3GetFbsPostingRequest { Posting_number = postingNumber }, ct)).Result;
        }
        catch (OzonApiException ex) when (ex.StatusCode == 404)
        {
            logger.LogWarning("Ozon no longer knows posting {PostingNumber}", postingNumber);
            // swallowed here rather than at the provider, so the body is written on the way past
            logger.LogFailedResponse(ex, LogLevel.Warning);
            return null;
        }

        if (posting is null)
            return null;

        return new ExternalPostingStatus(
            posting.Posting_number ?? postingNumber,
            ToOrderStatus(posting.Status),
            posting.Status,
            posting.Substatus,
            posting.Tracking_number);
    }

    public async Task<ExternalLabelDocument> GetPackageLabelAsync(
        IReadOnlyList<string> postingNumbers, CancellationToken ct)
    {
        using var response = await api.PostingAPI_PostingFBSPackageLabelAsync(
            new PostingPostingFBSPackageLabelRequest { Posting_number = [.. postingNumbers] }, ct);

        using var buffer = new MemoryStream();
        await response.Stream.CopyToAsync(buffer, ct);
        var bytes = buffer.ToArray();

        return ReadLabelPayload(bytes, postingNumbers);
    }

    /// <summary>
    /// The spec declares this response as `application/pdf` yet types it as a JSON envelope, so neither
    /// declaration is trusted: the bytes decide. A 200 carrying nothing is Ozon's cheapest way of saying
    /// "not yet".
    /// </summary>
    private ExternalLabelDocument ReadLabelPayload(byte[] bytes, IReadOnlyList<string> postingNumbers)
    {
        if (bytes.Length == 0)
            return NotReady();

        if (bytes.Length >= 4 && bytes[0] == '%' && bytes[1] == 'P' && bytes[2] == 'D' && bytes[3] == 'F')
            return new ExternalLabelDocument(true, postingNumbers, "application/pdf", bytes);

        var text = Encoding.UTF8.GetString(bytes);
        if (text.TrimStart().StartsWith('{'))
        {
            var envelope = JsonSerializer.Deserialize<LabelEnvelope>(text, LabelEnvelopeOptions);

            if (!string.IsNullOrWhiteSpace(envelope?.File_content))
                return new ExternalLabelDocument(
                    true,
                    postingNumbers,
                    envelope.Content_type ?? "application/pdf",
                    DecodeFileContent(envelope.File_content));

            if (OzonLabelHeuristics.LooksNotReady(text))
                return NotReady();
        }

        throw new MarketplaceApiException(
            "Ozon returned an unrecognized label payload.", null, Truncate(text));

        ExternalLabelDocument NotReady()
        {
            logger.LogInformation("Ozon has not produced labels for {Count} posting(s) yet", postingNumbers.Count);
            return new ExternalLabelDocument(false, postingNumbers, null, null);
        }
    }

    private byte[] DecodeFileContent(string fileContent)
    {
        try
        {
            return Convert.FromBase64String(fileContent);
        }
        catch (FormatException)
        {
            // the spec's own example inlines a raw PDF into this string rather than base64
            logger.LogWarning("Ozon label file_content is not base64; treating it as raw bytes");
            return Encoding.UTF8.GetBytes(fileContent);
        }
    }

    private static string Truncate(string text) => text[..Math.Min(text.Length, 2000)];

    private static readonly JsonSerializerOptions LabelEnvelopeOptions = new() { PropertyNameCaseInsensitive = true };

    private sealed record LabelEnvelope(string? File_content, string? File_name, string? Content_type);

    private ExternalPosting? ToExternalPosting(PostingFbsUnfulfilledListResponsePostings posting)
    {
        if (string.IsNullOrWhiteSpace(posting.Posting_number))
            return null;

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
            (posting.Products ?? [])
                .Select(p => new ExternalPostingItem(
                    p.Sku?.ToString(CultureInfo.InvariantCulture),
                    p.Offer_id ?? "",
                    p.Name ?? "",
                    p.Quantity ?? 0))
                .ToList());
    }

    /// <summary>Ozon posting states collapsed to the WMS vocabulary. Unknown values are logged, not guessed.</summary>
    private MarketplaceOrderStatus ToOrderStatus(string? status)
    {
        switch (status)
        {
            case AwaitingDeliver:
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
