using System.Globalization;
using System.Runtime.CompilerServices;
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
