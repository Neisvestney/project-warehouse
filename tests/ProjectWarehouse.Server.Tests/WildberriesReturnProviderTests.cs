using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Options = Microsoft.Extensions.Options.Options;
using Neisvestney.Marketplaces.Wildberries;
using ProjectWarehouse.Server.Infrastructure.Marketplaces;
using ProjectWarehouse.Server.Integrations.Abstractions;
using ProjectWarehouse.Server.Integrations.Wildberries;

namespace ProjectWarehouse.Server.Tests;

public class WildberriesReturnProviderTests
{
    [Fact]
    public async Task GetReturns_PreservesSeparateAttemptsOfSameExemplarAndLatestStatusOfEachAttempt()
    {
        using var handler = new ReturnsHandler();
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://wildberries.test/") };
        var client = new WildberriesClient(new WildberriesCommonClient(http), new WildberriesContentClient(http),
            new WildberriesMarketplaceClient(http), new WildberriesSellerAnalyticsClient(http),
            new WildberriesFinanceClient(http), new WildberriesStatisticsClient(http),
            Options.Create(new MarketplacesOptions()), NullLogger<WildberriesClient>.Instance);
        var query = ExternalReturnQuery.ByPeriod(ExternalReturnDateKind.StatusChanged,
            new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), DateTime.UtcNow);
        var returns = new List<ExternalReturn>();

        await foreach (var page in client.GetReturnsAsync(query, CancellationToken.None))
            returns.AddRange(page);

        Assert.Equal(2, returns.Count);
        Assert.All(returns, r => Assert.Equal("55619355515", r.ExemplarId));
        var first = Assert.Single(returns, r => r.SourceExternalId == "first-srid");
        var second = Assert.Single(returns, r => r.SourceExternalId == "second-srid");
        Assert.Equal(WildberriesReturnLinker.SellerAttemptPrefix + "first-srid", first.ExternalId);
        Assert.Equal(WildberriesReturnLinker.SellerAttemptPrefix + "second-srid", second.ExternalId);
        Assert.Equal(new DateOnly(2026, 7, 10), first.RequestedOn);
        Assert.Equal(new DateOnly(2026, 9, 25), second.RequestedOn);
        Assert.Equal("Issued", second.RawStatus);
        Assert.All(returns, r => Assert.Equal("", r.PostingNumber));
        Assert.Equal(3, handler.Requests);
    }

    private sealed class ReturnsHandler : HttpMessageHandler
    {
        public int Requests { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Requests++;
            var body = request.RequestUri!.AbsolutePath switch
            {
                "/api/analytics/v1/item-returns" => """
                    {"count":3,"report":[
                      {"shkId":55619355515,"srid":"first-srid","orderDt":"2026-07-10",
                       "readyToReturnDt":"2026-07-12T10:00:00","returnStatus":"Cancelled"},
                      {"shkId":55619355515,"srid":"second-srid","orderDt":"2026-09-25",
                       "readyToReturnDt":"2026-09-27T10:00:00","returnStatus":"Moving"},
                      {"shkId":55619355515,"srid":"second-srid","orderDt":"2026-09-25",
                       "readyToReturnDt":"2026-09-28T10:00:00","returnStatus":"Issued"}
                    ]}
                    """,
                "/content/v2/get/cards/list" or "/content/v2/get/cards/trash" => "{\"cards\":[]}",
                _ => throw new InvalidOperationException("Unexpected marketplace request: " + request.RequestUri),
            };
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            });
        }
    }
}
