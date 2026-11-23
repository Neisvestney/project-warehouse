using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Neisvestney.Marketplaces.YandexMarket;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using ProjectWarehouse.Server.Domain;
using ProjectWarehouse.Server.Infrastructure.Labels;
using ProjectWarehouse.Server.Infrastructure.Marketplaces;
using ProjectWarehouse.Server.Integrations.Abstractions;
using ProjectWarehouse.Server.Integrations.Ozon;
using ProjectWarehouse.Server.Integrations.YandexMarket;
using ProjectWarehouse.Server.Services;
using Options = Microsoft.Extensions.Options.Options;

namespace ProjectWarehouse.Server.Tests;

public class YandexMarketClientTests
{
    [Theory]
    [InlineData("USER_NOT_PAID", MarketplaceLossReason.BuyerOther)]
    [InlineData("REPLACING_ORDER", MarketplaceLossReason.BuyerChangedMind)]
    [InlineData("UNRECOGNIZED", MarketplaceLossReason.Unknown)]
    public void Cancellation_MapsBuyerInitiatedReasons(string code, MarketplaceLossReason expected)
        => Assert.Equal(expected, YandexMarketClient.LossReason(code));

    [Theory]
    [InlineData("Возврат премии", "Денежная премия за участие в совместных акциях", -1898.99, MarketplaceAccrualCategory.Compensation)]
    [InlineData("Премия", "Денежная премия за участие в совместных акциях", 1898.99, MarketplaceAccrualCategory.Compensation)]
    [InlineData("Возврат скидки за участие в совместных акциях", "Скидка за лояльность", 9084.54, MarketplaceAccrualCategory.Commission)]
    [InlineData("Оплата услуг Маркета", "Скидка за лояльность", 2420.82, MarketplaceAccrualCategory.Commission)]
    [InlineData("Оплата услуг Маркета", null, -470, MarketplaceAccrualCategory.Services)]
    [InlineData("Оплата услуг Маркета", "", -470, MarketplaceAccrualCategory.Services)]
    [InlineData("Оплата услуг Маркета", "Unknown service", -1, MarketplaceAccrualCategory.Other)]
    [InlineData("Unknown source", null, -1, MarketplaceAccrualCategory.Other)]
    public async Task Accruals_ClassifyLoyaltyAndPremiumAdjustmentsWithoutChangingMoney(string source,
        string? service, double amount, MarketplaceAccrualCategory expected)
    {
        var json = JsonSerializer.Serialize(new[] { new { transactionSource = source, offerOrServiceName = service,
            transactionType = "adjustment", transactionDate = "20.11.2026 12:30", transactionSum = amount } });
        var line = Assert.Single(Assert.Single(await Parse(json)).Lines);
        Assert.Equal(expected, line.Category);
        Assert.Equal((decimal)amount, line.Amount);
    }

    [Fact]
    public async Task CampaignDiscovery_PagesFiltersAndValidatesSelectedBusiness()
    {
        using var handler = new Handler((request, index) => Json(index == 1 ? """
            {"campaigns":[
              {"id":20,"domain":"Shop","business":{"id":10,"name":"Business"},"placementType":"FBS","apiAvailability":"AVAILABLE"},
              {"id":21,"business":{"id":10},"placementType":"DBS","apiAvailability":"AVAILABLE"}],
             "paging":{"nextPageToken":"next"}}
            """ : """
            {"campaigns":[{"id":22,"business":{"id":10},"placementType":"FBS","apiAvailability":"MANUALLY_DISABLED"}]}
            """));
        var (client, context) = Client(handler);
        var service = new YandexMarketConnectionService(client, context);
        var (campaigns, problem) = await service.DiscoverAsync("key", default);
        Assert.Null(problem);
        Assert.Equal(20, Assert.Single(campaigns).CampaignId);
        Assert.Contains("pageToken=next", handler.Requests[1].Url);
        var invalid = await service.ValidateAsync("key", 20, 99, default);
        Assert.NotNull(invalid.Problem);
    }

    [Fact]
    public async Task Warehouses_RespectThirtyRowLimitAndFilterCampaignOnEveryPage()
    {
        using var handler = new Handler((request, index) => Json(index == 1 ? """
            {"result":{"warehouses":[
              {"id":1,"name":"Selected","campaignId":20,"express":false},
              {"id":2,"name":"Other campaign","campaignId":21,"express":false}],
             "paging":{"nextPageToken":"next"}}}
            """ : """
            {"result":{"warehouses":[{"id":3,"name":"Express","campaignId":20,"express":true}]}}
            """));
        var (client, context) = Client(handler, authenticate: true);
        using var scope = context.Use(new(null, "key", 10, 20));
        var warehouses = await client.GetWarehousesAsync(default);
        Assert.Equal(new[] {"1", "3"}, warehouses.Select(w => w.ExternalId));
        Assert.Equal(MarketplaceWarehouseKind.Express, warehouses[1].Kind);
        Assert.Equal(2, handler.Requests.Count);
        Assert.All(handler.Requests, request =>
        {
            var query = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(new Uri(request.Url).Query);
            Assert.Equal("30", query["limit"].ToString());
            Assert.Equal("key", request.ApiKey);
        });
        Assert.Contains("pageToken=next", handler.Requests[1].Url);
    }

    [Fact]
    public async Task Cards_KeepCredentialsOnEveryPageAndArchiveBySelectedCampaign()
    {
        using var handler = new Handler((request, index) => Json(index == 1 ? """
            {"result":{"offerMappings":[{"offer":{"offerId":"sku","name":"Product","campaigns":[{"campaignId":20,"status":"NO_STOCKS"}]},"mapping":{"marketSku":123}}],"paging":{"nextPageToken":"next"}}}
            """ : """
            {"result":{"offerMappings":[{"offer":{"offerId":"other","name":"Other","campaigns":[{"campaignId":21,"status":"PUBLISHED"}]}}]}}
            """));
        var (client, context) = Client(handler, authenticate: true);
        var provider = new YandexMarketMarketplaceProvider(client, context, NullLogger<YandexMarketMarketplaceProvider>.Instance);
        var cards = new List<ExternalCard>();
        await foreach (var page in provider.FetchCardsAsync(new(null, "key", 10, 20), default)) cards.AddRange(page);
        Assert.Equal(2, cards.Count);
        Assert.False(cards[0].IsArchived);
        Assert.True(cards[1].IsArchived);
        Assert.All(handler.Requests, r => Assert.Equal("key", r.ApiKey));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Orders_ScopeCampaignAndUseCorrectDateFilters(bool changed)
    {
        var since = new DateTime(2026, 9, 1, 21, 0, 0, DateTimeKind.Utc);
        var to = new DateTime(2026, 11, 23, 23, 0, 0, DateTimeKind.Utc);
        using var handler = new Handler((request, index) => Json("{\"orders\":[]}"));
        var (client, context) = Client(handler);
        using var scope = context.Use(new(null, "key", 10, 20));
        await foreach (var page in client.GetPostingsAsync(new(ExternalPostingScheme.Fbs, since, to,
            StatusChangedSince: changed ? since : null), default)) { }
        Assert.True(handler.Requests.Count >= 3);
        foreach (var request in handler.Requests)
        {
            Assert.EndsWith("/v1/businesses/10/orders?limit=50", request.Url);
            using var body = JsonDocument.Parse(request.Body!);
            Assert.Equal(20, body.RootElement.GetProperty("campaignIds")[0].GetInt64());
            Assert.False(body.RootElement.GetProperty("fake").GetBoolean());
            var dates = body.RootElement.GetProperty("dates");
            var startName = changed ? "updateDateFrom" : "creationDateFrom";
            var endName = changed ? "updateDateTo" : "creationDateTo";
            var from = DateTimeOffset.Parse(dates.GetProperty(startName).GetString()!);
            var end = DateTimeOffset.Parse(dates.GetProperty(endName).GetString()!);
            Assert.True(end - from <= TimeSpan.FromDays(30));
            Assert.True(!dates.TryGetProperty(changed ? "creationDateFrom" : "updateDateFrom", out var unused)
                || unused.ValueKind == JsonValueKind.Null);
        }
        using var final = JsonDocument.Parse(handler.Requests[^1].Body!);
        var finalDates = final.RootElement.GetProperty("dates");
        if (changed)
            Assert.Equal(to, finalDates.GetProperty("updateDateTo").GetDateTimeOffset().UtcDateTime);
        else
            Assert.Equal("2026-11-25", finalDates.GetProperty("creationDateTo").GetString());
    }

    [Fact]
    public void Posting_MapsRepeatedOffersBySourceItemIdAndConvertsLineTotalsToUnitPrices()
    {
        using var handler = new Handler((request, index) => Json("{}"));
        var (client, _) = Client(handler);
        var order = new BusinessOrderDTO
        {
            OrderId = 100, CampaignId = 20, Status = OrderStatusType.PROCESSING,
            Substatus = OrderSubstatusType.READY_TO_SHIP,
            CreationDate = DateTimeOffset.UtcNow,
            Items = [new() { Id = 1, OfferId = "same", OfferName = "First", Count = 2,
                Prices = new() { Payment = new() { Value = 200, CurrencyId = CurrencyType.RUR },
                    Cashback = new() { Value = 10 }, Subsidy = new() { Value = 20 } } },
                new() { Id = 2, OfferId = "same", OfferName = "Second", Count = 1 }],
            Delivery = new() { ServiceName = "Market", Dates = new(), BoxesLayout = [
                new() {BoxId = 7, Barcode = "100-1", Items = [new() {Id = 2, FullCount = 1}]},
                new() {BoxId = 8, Barcode = "100-2", Items = [new() {Id = 1, PartialCount = new() {Current = 1, Total = 2}}]}] },
        };
        var posting = client.MapPosting(order);
        Assert.Equal(105, posting.Items[0].CustomerPrice);
        Assert.Equal(115, posting.Items[0].Price);
        Assert.Equal("1", posting.Items[0].ExternalItemId);
        Assert.Equal("2", posting.Items[1].ExternalItemId);
        Assert.Equal(1, posting.Parcels![0].Items[0].ItemIndex);
        Assert.Null(posting.Parcels[1].Items[0].Quantity);
        Assert.Equal(2, posting.Parcels[1].Items[0].PartCount);
        Assert.Equal(MarketplaceOrderStatus.AwaitingDeliver, posting.Status);
    }

    [Theory]
    [InlineData(ReturnType.UNREDEEMED, RefundStatusType.REFUNDED, ReturnShipmentStatusType.PICKED, false, MarketplaceReturnKind.FullRefusal)]
    [InlineData(ReturnType.RETURN, RefundStatusType.REJECTED, ReturnShipmentStatusType.CREATED, true, MarketplaceReturnKind.CustomerReturn)]
    public void Return_ExpandsItemsAndPreservesMoneyStatus(ReturnType type, RefundStatusType refund,
        ReturnShipmentStatusType shipment, bool cancelled, MarketplaceReturnKind kind)
    {
        var rows = YandexMarketClient.MapReturn(new ReturnDTO { Id = 5, OrderId = 100, ReturnType = type,
            RefundStatus = refund, ShipmentStatus = shipment, Items = [new() {ShopSku = "one", Count = 2},
                new() {ShopSku = "two", Count = 1}] }).ToArray();
        Assert.Equal(2, rows.Length);
        Assert.Equal("5:one", rows[0].ExternalId);
        Assert.Equal(2, rows[0].Quantity);
        Assert.All(rows, r => { Assert.Equal(kind, r.Kind); Assert.Equal(cancelled, r.IsCancelled);
            Assert.Equal(refund.ToString(), r.RawRefundStatus); Assert.Null(r.Price); });
    }

    [Fact]
    public async Task ReturnHistory_ReadsLateUpdatesThroughToday()
    {
        using var handler = new Handler((request, index) => Json("{\"result\":{\"returns\":[]}}"));
        var (client, context) = Client(handler);
        using var scope = context.Use(new(null, "key", 10, 20));
        await foreach (var page in client.GetReturnsAsync(ExternalReturnQuery.ByPeriod(ExternalReturnDateKind.Returned,
            new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), new(2026, 1, 31, 0, 0, 0, DateTimeKind.Utc)), default)) { }
        Assert.Contains("toDate=" + DateTime.UtcNow.ToString("yyyy-MM-dd"), Assert.Single(handler.Requests).Url);
    }

    [Fact]
    public async Task Labels_ReturnEveryParcelWithItsOwnBarcodeAndNoGlobalPageGuessing()
    {
        using var handler = new Handler((request, index) => Json("""
            {"result":{"orderId":100,"placesNumber":2,"parcelBoxLabels":[
              {"orderId":100,"boxId":7,"fulfilmentId":"100-1","url":"https://api.partner.market.yandex.ru/labels/7?format=A7&other=value"},
              {"orderId":100,"boxId":8,"fulfilmentId":"100-2","url":"https://api.partner.market.yandex.ru/labels/8"}]}}
            """));
        using var downloads = new Handler((request, index) => new(HttpStatusCode.OK) {Content = new ByteArrayContent(Pdf())});
        var (client, context) = Client(handler, downloads: downloads);
        using var scope = context.Use(new(null, "key", 10, 20));
        var document = await client.GetLabelsAsync(["100"], default);
        var pages = Assert.IsAssignableFrom<IReadOnlyList<ExternalLabelPage>>(document.Pages);
        Assert.Equal(new[] {"7", "8"}, pages.Select(p => p.ParcelExternalId));
        Assert.Equal(new[] {"100-1", "100-2"}, pages.Select(p => p.Barcode));
        Assert.All(pages, p => Assert.Equal(1, LabelPdfComposer.PageCount(p.Content)));
        Assert.All(downloads.Requests, r => Assert.Equal("key", r.ApiKey));
        Assert.All(downloads.Requests, r => Assert.Contains("format=A9_HORIZONTALLY", r.Url));
        Assert.DoesNotContain("format=A7", downloads.Requests[0].Url);
        Assert.Contains("other=value", downloads.Requests[0].Url);
        foreach (var label in pages)
        {
            using var pdf = PdfReader.Open(new MemoryStream(label.Content), PdfDocumentOpenMode.Import);
            Assert.Equal(58, pdf.Pages[0].Width.Millimeter, 2);
            Assert.Equal(40, pdf.Pages[0].Height.Millimeter, 2);
        }
    }

    [Fact]
    public async Task Labels_RejectUntrustedHostBeforeSendingKey()
    {
        using var handler = new Handler((request, index) => Json("""
            {"result":{"orderId":100,"parcelBoxLabels":[{"orderId":100,"boxId":7,"url":"https://attacker.example/label"}]}}
            """));
        using var downloads = new Handler((request, index) => throw new InvalidOperationException("No download expected."));
        var (client, context) = Client(handler, downloads: downloads);
        using var scope = context.Use(new(null, "key", 10, 20));
        await Assert.ThrowsAsync<InvalidOperationException>(() => client.GetLabelsAsync(["100"], default));
        Assert.Empty(downloads.Requests);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Accruals_GroupDeterministicallyPreserveBonusSignsAndKeepTopupMetadata(bool wrapped)
    {
        var rows = """
            [{"inn":"1234567890","orderId":100,"shopSku":"offer","transactionDate":"20.11.2026 12:30","transactionType":"Начисление","transactionSource":"Платёж покупателя","transactionSum":200},
             {"inn":"1234567890","orderId":100,"shopSku":"offer","transactionDate":"20.11.2026 12:30","transactionType":"Начисление","transactionSource":"Баллы за скидку Маркета","transactionSum":20},
             {"inn":"1234567890","orderId":100,"shopSku":"offer","transactionDate":"20.11.2026 13:00","transactionType":"Списание","transactionSource":"Скидка за участие в совместных акциях","offerOrServiceName":"Размещение товарных предложений","transactionSum":-20}]
            """;
        string Sheet(string values) => wrapped ? "{\"rows\":" + values + "}" : values;
        var parsed = await Parse(Sheet(rows));
        var group = Assert.Single(parsed);
        Assert.Equal("100:2026-11-20", group.ExternalId);
        Assert.Equal(200, group.Lines.Sum(l => l.Amount));
        Assert.Equal(220, group.Lines.Where(l => l.Category == MarketplaceAccrualCategory.Sale).Sum(l => l.Amount));
        using var json = JsonDocument.Parse(rows);
        var reversed = "[" + string.Join(',', json.RootElement.EnumerateArray().Reverse().Select(e => e.GetRawText())) + "]";
        Assert.Equal(group, Assert.Single(await Parse(Sheet(reversed))) with {Lines = group.Lines});
        Assert.Equal(group.Lines, Assert.Single(await Parse(Sheet(reversed))).Lines);
        var metadata = Assert.Single(await Parse(Sheet("""
            [{"inn":"1234567890","transactionType":"Начисление","transactionSource":"Внесено продавцом","transactionSum":100}]
            """)));
        Assert.True(metadata.IsMetadata);
        Assert.Empty(metadata.Lines);
        Assert.Equal("1234567890", metadata.SellerInn);
        await Assert.ThrowsAsync<InvalidDataException>(() => Parse("{\"unexpected\":[]}"));
        await Assert.ThrowsAsync<InvalidDataException>(() => Parse("{\"rows\":{}}"));
        Assert.Empty(await Parse(Sheet("[]")));
    }

    [Fact]
    public async Task Accruals_RefusedReportIsNotRetriedAndKeepsCampaignScope()
    {
        using var handler = new Handler((request, index) => new((HttpStatusCode)420)
            {Content = new StringContent("{\"errors\":[{\"code\":\"LIMIT\",\"message\":\"Quota exceeded\"}]}")});
        var (client, context) = Client(handler);
        var provider = new YandexMarketMarketplaceProvider(client, context,
            NullLogger<YandexMarketMarketplaceProvider>.Instance);
        var error = await Assert.ThrowsAsync<MarketplaceApiException>(async () =>
        {
            await foreach (var page in provider.FetchAccrualsAsync(new(null, "key", 10, 20),
                new(2026, 1, 1), new(2026, 1, 31), default)) { }
        });
        Assert.Equal(420, error.StatusCode);
        var request = Assert.Single(handler.Requests);
        Assert.Contains("format=JSON", request.Url);
        Assert.Contains("language=RU", request.Url);
        using var body = JsonDocument.Parse(request.Body!);
        Assert.Equal(10, body.RootElement.GetProperty("businessId").GetInt64());
        Assert.Equal(20, Assert.Single(body.RootElement.GetProperty("campaignIds").EnumerateArray()).GetInt64());
    }

    [Fact]
    public async Task ReportQuota_IsSharedByBusinessAndCancellationDoesNotReserveAnotherSlot()
    {
        var quota = new YandexMarketReportQuota(Options.Create(new MarketplacesOptions()));
        await quota.WaitAsync(10, default);
        using var timeout = new CancellationTokenSource(TimeSpan.FromMilliseconds(30));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => quota.WaitAsync(10, timeout.Token));
        using var independent = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        await quota.WaitAsync(11, independent.Token);
    }

    private static Task<IReadOnlyList<ExternalAccrual>> Parse(string json) =>
        YandexMarketAccrualParser.ParseAsync(new MemoryStream(Encoding.UTF8.GetBytes(json)), 1024 * 1024, default);

    private static (YandexMarketClient Client, MarketplaceRequestContext Context) Client(Handler handler,
        bool authenticate = false, Handler? downloads = null)
    {
        var context = new MarketplaceRequestContext();
        HttpMessageHandler pipeline = authenticate
            ? new YandexMarketAuthHandler(new YandexMarketRequestCredentials(context)) {InnerHandler = handler} : handler;
        var http = new HttpClient(pipeline) {BaseAddress = new Uri("https://api.partner.market.yandex.ru")};
        var options = Options.Create(new MarketplacesOptions {YandexMarket = new() {PageDelayMs = 0}});
        return (new(new YandexMarketApiClient(http), context, new Factory(new HttpClient(downloads ?? handler)),
            new(options), options, NullLogger<YandexMarketClient>.Instance), context);
    }

    private static HttpResponseMessage Json(string body) => new(HttpStatusCode.OK)
        {Content = new StringContent(body, Encoding.UTF8, "application/json")};

    private static byte[] Pdf()
    {
        using var pdf = new PdfDocument();
        pdf.AddPage();
        using var output = new MemoryStream();
        pdf.Save(output, false);
        return output.ToArray();
    }

    private sealed record Request(string Url, string? Body, string? ApiKey);
    private sealed class Handler(Func<HttpRequestMessage, int, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<Request> Requests { get; } = [];
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Requests.Add(new(request.RequestUri!.ToString(), request.Content is null ? null : await request.Content.ReadAsStringAsync(ct),
                request.Headers.TryGetValues("Api-Key", out var keys) ? keys.Single() : null));
            return respond(request, Requests.Count);
        }
    }

    private sealed class Factory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }
}
