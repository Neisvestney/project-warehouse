using System.Security.Claims;
using Microsoft.Extensions.DependencyInjection;
using ProjectWarehouse.Server.Data;
using ProjectWarehouse.Server.Domain;
using ProjectWarehouse.Server.Integrations.Abstractions;
using ProjectWarehouse.Server.Models.Analytics;
using ProjectWarehouse.Server.Services;

namespace ProjectWarehouse.Server.Tests;

public class AnalyticsPayoutDiscountTests(TokenDatabase database) : IClassFixture<TokenDatabase>
{
    [PostgresTheory]
    [InlineData(100)]
    [InlineData(500)]
    public async Task Payouts_AggregatesKnownDiscountsPreservesUnknownAndExcludesDiscountFromNet(int reversal)
    {
        await using var scope = database.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var first = Account();
        var second = Account();
        var known = Journal(first, MarketplaceAccrualCategory.Sale, 500, 125);
        var reversed = Journal(first, MarketplaceAccrualCategory.Sale, -reversal, -25);
        var fee = Journal(first, MarketplaceAccrualCategory.Commission, -200, null);
        var unknown = Journal(second, MarketplaceAccrualCategory.Sale, 600, null);
        var buyout = Journal(second, MarketplaceAccrualCategory.Sale, 300, null);
        buyout.Source = MarketplaceAccrualSource.BuyoutReport;
        var outside = Journal(first, MarketplaceAccrualCategory.Sale, 1000, 1000);
        outside.Date = new(2025, 12, 1);
        db.AddRange(known, reversed, fee, unknown, buyout, outside);
        await db.SaveChangesAsync();

        var service = new AnalyticsPayoutsService(db, new AnalyticsQueries(db, new UtcTimeZone()),
            new AnalyticsSettingsService(db), new MarketplaceProviderRegistry([]));
        var result = await service.GetPayoutsAsync(new ClaimsPrincipal(), new PayoutsRequest
        {
            From = new(2026, 1, 1), To = new(2026, 1, 31), MarketplaceAccountIds = [first.Id, second.Id],
        });

        var firstMoney = Assert.Single(result.Rows.Single(r => r.MarketplaceAccountId == first.Id).Money);
        var firstSale = Assert.Single(firstMoney.Categories, c => c.Category == MarketplaceAccrualCategory.Sale);
        Assert.Equal(100m, firstSale.PlatformDiscountAmount);
        Assert.Equal(500m - reversal, firstSale.Amount);
        Assert.Equal(300m - reversal, firstMoney.Accrued);
        var secondMoney = Assert.Single(result.Rows.Single(r => r.MarketplaceAccountId == second.Id).Money);
        Assert.All(secondMoney.Categories, c => Assert.Null(c.PlatformDiscountAmount));

        var total = Assert.Single(result.Totals);
        var totalSale = Assert.Single(total.Categories,
            c => c.Category == MarketplaceAccrualCategory.Sale && c.Source == MarketplaceAccrualSource.AccrualJournal);
        Assert.Equal(100m, totalSale.PlatformDiscountAmount);
        Assert.Equal(1100m - reversal, totalSale.Amount);
        Assert.Equal(1200m - reversal, total.Accrued);
        Assert.Null(Assert.Single(total.Categories,
            c => c.Category == MarketplaceAccrualCategory.Commission).PlatformDiscountAmount);
        Assert.Null(Assert.Single(total.Categories,
            c => c.Source == MarketplaceAccrualSource.BuyoutReport).PlatformDiscountAmount);
    }

    private static MarketplaceAccount Account() => new()
    {
        Id = Guid.NewGuid(), Type = MarketplaceType.Wildberries, Name = "Discount test",
        ApiKeyProtected = "unused", ApiKeyLast4 = "test",
    };

    private static MarketplaceAccrual Journal(MarketplaceAccount account, MarketplaceAccrualCategory category,
        decimal amount, decimal? discount) => new()
    {
        MarketplaceAccount = account, ExternalId = Guid.NewGuid().ToString("N"), Date = new(2026, 1, 5),
        Scope = MarketplaceAccrualScope.Account, Source = MarketplaceAccrualSource.AccrualJournal,
        Category = category, Amount = amount, PlatformDiscountAmount = discount, CurrencyCode = "RUB",
    };

    private sealed class UtcTimeZone : IWarehouseTimeZoneResolver
    {
        public Task<TimeZoneInfo> ResolveAsync(Guid? warehouseId, CancellationToken ct = default) =>
            Task.FromResult(TimeZoneInfo.Utc);
    }
}
