using System.Security.Claims;
using Microsoft.Extensions.DependencyInjection;
using ProjectWarehouse.Server.Data;
using ProjectWarehouse.Server.Domain;
using ProjectWarehouse.Server.Infrastructure;
using ProjectWarehouse.Server.Infrastructure.Access;
using ProjectWarehouse.Server.Services;

namespace ProjectWarehouse.Server.Tests;

public class OrderFinanceServiceTests(TokenDatabase database) : IClassFixture<TokenDatabase>
{
    [PostgresTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task History_UsesSameAccountAndExemplarAndFiltersInaccessibleOrders(bool restricted)
    {
        await using var scope = database.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var account = Account();
        var warehouse = new Warehouse { Id = Guid.NewGuid(), Name = "Accessible warehouse" };
        var user = new ApplicationUser { Id = Guid.NewGuid(), UserName = Guid.NewGuid().ToString("N") };
        user.AssignedWarehouses.Add(warehouse);
        var current = Order(account, "current", "unit");
        current.Warehouse = warehouse;
        var linked = Order(account, "linked", null);
        var labelOnly = Order(account, "label-only", "unit");
        var otherAccount = Account();
        var foreign = Order(otherAccount, "foreign", "unit");
        var sale = Accrual(account, current, "unit", 500);
        sale.Category = MarketplaceAccrualCategory.Sale;
        sale.Date = new DateOnly(2026, 9, 1);
        var fee = Accrual(account, current, null, -25);
        var linkedFee = Accrual(account, linked, "unit", -35);
        var unlinked = Accrual(account, null, "unit", -10);
        var returned = Return(account, linked, "unit");
        returned.RequestedOn = new DateOnly(2026, 9, 3);
        returned.SaleReversedOn = new DateOnly(2026, 9, 5);
        var unlinkedReturn = Return(account, null, "unit");
        db.AddRange(user, sale, fee, linkedFee, unlinked, returned, unlinkedReturn, labelOnly,
            Accrual(otherAccount, foreign, "unit", 1000), Accrual(account, null, "different", 2000));
        await db.SaveChangesAsync();

        var principal = Principal(user.Id, restricted ? Permissions.Orders.ViewAssigned : Permissions.Orders.View);
        var service = new OrderFinanceService(db, new EntityAccessRegistry(db, new AccessScope(db)));
        var result = await service.GetAsync(principal, current);

        Assert.Equal(new[] { sale.Id, fee.Id }.Order(), result.Accruals.Select(a => a.Id).Order());
        Assert.All(result.Accruals, a => Assert.Null(a.ItemName));
        var exemplar = Assert.Single(result.Exemplars);
        Assert.Equal("unit", exemplar.ExemplarId);
        Assert.Equal(restricted, exemplar.HasRestrictedHistory);
        var expectedOrders = restricted ? new[] { current.Id } : new[] { current.Id, linked.Id, labelOnly.Id };
        Assert.Equal(expectedOrders.Order(), exemplar.Orders.Select(o => o.Id).Order());
        var expectedAccruals = restricted ? new[] { sale.Id, unlinked.Id } : new[] { sale.Id, linkedFee.Id, unlinked.Id };
        Assert.Equal(expectedAccruals.Order(), exemplar.Accruals.Select(a => a.Id).Order());
        var expectedReturns = restricted ? new[] { unlinkedReturn.Id } : new[] { returned.Id, unlinkedReturn.Id };
        Assert.Equal(expectedReturns.Order(), exemplar.Returns.Select(r => r.Id).Order());
        Assert.Null(exemplar.Accruals.Single(a => a.Id == unlinked.Id).OrderId);
        Assert.All(exemplar.Accruals, a => Assert.Null(a.OccurredAt));
        Assert.All(exemplar.Orders, o =>
        {
            Assert.Null(o.InProcessAt);
            Assert.Null(o.DeliveredAt);
            Assert.Null(o.CancelledAt);
        });
        if (!restricted)
        {
            var persistedReturn = exemplar.Returns.Single(r => r.Id == returned.Id);
            Assert.Equal(returned.RequestedOn, persistedReturn.RequestedOn);
            Assert.Equal(returned.SaleReversedOn, persistedReturn.SaleReversedOn);
            Assert.Null(persistedReturn.ReturnedAt);
            Assert.Null(persistedReturn.FinalAt);
        }
    }

    [PostgresFact]
    public async Task History_LabelSeedsExemplarWithoutCurrentAccrualsAndOtherProvidersHaveNoExemplarHistory()
    {
        await using var scope = database.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var account = Account();
        var current = Order(account, "current", "unit");
        var fee = Accrual(account, null, "unit", -10);
        var ozon = Account();
        ozon.Type = MarketplaceType.Ozon;
        var ozonOrder = Order(ozon, "ozon", "unit");
        var ozonSale = Accrual(ozon, ozonOrder, "unit", 100);
        db.AddRange(current, fee, ozonSale);
        await db.SaveChangesAsync();
        var service = new OrderFinanceService(db, new EntityAccessRegistry(db, new AccessScope(db)));
        var principal = Principal(Guid.NewGuid(), Permissions.Orders.View);

        var history = await service.GetAsync(principal, current);

        Assert.Empty(history.Accruals);
        var exemplar = Assert.Single(history.Exemplars);
        Assert.Equal(current.Id, Assert.Single(exemplar.Orders).Id);
        Assert.Equal(fee.Id, Assert.Single(exemplar.Accruals).Id);
        Assert.False(exemplar.HasRestrictedHistory);
        var ozonFinance = await service.GetAsync(principal, ozonOrder);
        Assert.Equal(ozonSale.Id, Assert.Single(ozonFinance.Accruals).Id);
        Assert.Empty(ozonFinance.Exemplars);
    }

    private static ClaimsPrincipal Principal(Guid id, string permission) => new(new ClaimsIdentity(
        [new Claim("sub", id.ToString()), new Claim("permission", permission)], "test"));

    private static MarketplaceAccount Account() => new()
    {
        Id = Guid.NewGuid(), Type = MarketplaceType.Wildberries, Name = "Finance test",
        ApiKeyProtected = "unused", ApiKeyLast4 = "test",
    };

    private static Order Order(MarketplaceAccount account, string posting, string? label) => new()
    {
        Id = Guid.NewGuid(), Type = OrderType.FBS, Status = OrderStatus.Shipped,
        MarketplaceOrder = new MarketplaceOrder
        {
            MarketplaceAccount = account, PostingNumber = posting, LabelBarcode = label,
            Status = MarketplaceOrderStatus.Delivered,
        },
    };

    private static MarketplaceAccrual Accrual(MarketplaceAccount account, Order? order, string? exemplar, decimal amount) => new()
    {
        Id = Guid.NewGuid(), MarketplaceAccount = account, Order = order, ExemplarId = exemplar,
        ExternalId = Guid.NewGuid().ToString("N"), Date = new DateOnly(2026, 9, 2),
        Category = MarketplaceAccrualCategory.Logistics, Amount = amount, CurrencyCode = "RUB",
    };

    private static MarketplaceReturn Return(MarketplaceAccount account, Order? order, string exemplar) => new()
    {
        Id = Guid.NewGuid(), MarketplaceAccount = account, Order = order, ExemplarId = exemplar,
        ExternalId = Guid.NewGuid().ToString("N"), PostingNumber = order?.MarketplaceOrder!.PostingNumber ?? "",
        OfferId = "offer", Quantity = 1, Kind = MarketplaceReturnKind.CustomerReturn,
    };
}
