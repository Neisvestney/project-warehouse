using System.Linq.Expressions;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using ProjectWarehouse.Server.Data;
using ProjectWarehouse.Server.Domain;
using ProjectWarehouse.Server.Infrastructure.Access;
using ProjectWarehouse.Server.Models.Orders;

namespace ProjectWarehouse.Server.Services;

public class OrderFinanceService(ApplicationDbContext db, EntityAccessRegistry access) : IOrderFinanceService
{
    private static readonly Expression<Func<MarketplaceAccrual, OrderFinanceAccrualDto>> AccrualProjection = a =>
        new OrderFinanceAccrualDto
        {
            Id = a.Id,
            ExternalId = a.ExternalId,
            Date = a.Date,
            OccurredAt = a.OccurredAt,
            Category = a.Category,
            Source = a.Source,
            RawTypeId = a.RawTypeId,
            UnitNumber = a.UnitNumber,
            Rid = a.Rid,
            ExemplarId = a.ExemplarId,
            Sku = a.Sku,
            Amount = a.Amount,
            CurrencyCode = a.CurrencyCode,
            OrderId = a.OrderId,
            OrderNumber = a.Order != null ? a.Order.Number : null,
            PostingNumber = a.Order != null && a.Order.MarketplaceOrder != null
                ? a.Order.MarketplaceOrder.PostingNumber : null,
            ItemName = a.CatalogItem != null ? a.CatalogItem.FullName
                : a.OrderMarketplaceItem != null && a.OrderMarketplaceItem.MarketplaceCard != null
                    ? a.OrderMarketplaceItem.MarketplaceCard.Name : null,
        };

    public async Task<OrderFinanceDto> GetAsync(ClaimsPrincipal user, Order order, CancellationToken ct = default)
    {
        var accruals = await db.MarketplaceAccruals.AsNoTracking().Where(a => a.OrderId == order.Id)
            .OrderBy(a => a.Date).ThenBy(a => a.OccurredAt).ThenBy(a => a.ExternalId).ThenBy(a => a.LineNo)
            .Select(AccrualProjection).ToListAsync(ct);
        if (order.MarketplaceOrder?.MarketplaceAccount.Type != MarketplaceType.Wildberries)
            return new OrderFinanceDto { Accruals = accruals };

        var accountId = order.MarketplaceOrder.MarketplaceAccountId;
        var returnExemplars = await db.MarketplaceReturns.AsNoTracking()
            .Where(r => r.OrderId == order.Id && r.MarketplaceAccountId == accountId && r.ExemplarId != null)
            .Select(r => r.ExemplarId!).ToListAsync(ct);
        var exemplarIds = accruals.Select(a => a.ExemplarId)
            .Concat(returnExemplars)
            .Append(order.MarketplaceOrder.LabelBarcode)
            .Where(id => !string.IsNullOrWhiteSpace(id)).Select(id => id!).Distinct().Order().ToArray();
        if (exemplarIds.Length == 0)
            return new OrderFinanceDto { Accruals = accruals };

        var journalQuery = db.MarketplaceAccruals.AsNoTracking().Where(a =>
            a.MarketplaceAccountId == accountId && a.ExemplarId != null && exemplarIds.Contains(a.ExemplarId));
        var returnQuery = db.MarketplaceReturns.AsNoTracking().Where(r =>
            r.MarketplaceAccountId == accountId && r.ExemplarId != null && exemplarIds.Contains(r.ExemplarId));
        var labelQuery = db.MarketplaceOrders.AsNoTracking().Where(m =>
            m.MarketplaceAccountId == accountId && m.LabelBarcode != null && exemplarIds.Contains(m.LabelBarcode));

        var links = await journalQuery.Where(a => a.OrderId != null)
            .Select(a => new { ExemplarId = a.ExemplarId!, OrderId = a.OrderId!.Value })
            .Concat(returnQuery.Where(r => r.OrderId != null)
                .Select(r => new { ExemplarId = r.ExemplarId!, OrderId = r.OrderId!.Value }))
            .Concat(labelQuery.Select(m => new { ExemplarId = m.LabelBarcode!, m.OrderId }))
            .Distinct().ToListAsync(ct);
        var linkedOrderIds = links.Select(l => l.OrderId).Distinct().ToArray();
        var accessible = await access.For<Order>().QueryAsync(user, AccessLevel.View, ct);
        var orders = await accessible.AsNoTracking().Where(o => linkedOrderIds.Contains(o.Id) &&
                o.MarketplaceOrder != null && o.MarketplaceOrder.MarketplaceAccountId == accountId)
            .OrderBy(o => o.MarketplaceOrder!.InProcessAt).ThenBy(o => o.Number)
            .Select(o => new OrderExemplarOrderDto
            {
                Id = o.Id,
                Number = o.Number,
                PostingNumber = o.MarketplaceOrder!.PostingNumber,
                Type = o.Type,
                Status = o.MarketplaceOrder.Status,
                InProcessAt = o.MarketplaceOrder.InProcessAt,
                DeliveringAt = o.MarketplaceOrder.DeliveringAt,
                DeliveredAt = o.MarketplaceOrder.DeliveredAt,
                CancelledAt = o.MarketplaceOrder.CancelledAt,
                IsReconstructed = o.MarketplaceOrder.IsReconstructed,
            }).ToListAsync(ct);
        var visibleIds = orders.Select(o => o.Id).ToArray();
        var visibleIdSet = visibleIds.ToHashSet();
        var journal = await journalQuery.Where(a => a.OrderId == null || visibleIds.Contains(a.OrderId.Value))
            .OrderBy(a => a.Date).ThenBy(a => a.OccurredAt).ThenBy(a => a.ExternalId).ThenBy(a => a.LineNo)
            .Select(AccrualProjection).ToListAsync(ct);
        var returns = await returnQuery.Where(r => r.OrderId == null || visibleIds.Contains(r.OrderId.Value))
            .OrderBy(r => r.RequestedOn).ThenBy(r => r.ReturnedAt).ThenBy(r => r.Id)
            .Select(r => new
            {
                ExemplarId = r.ExemplarId!,
                Return = new OrderExemplarReturnDto
                {
                    Id = r.Id,
                    OrderId = r.OrderId,
                    Kind = r.Kind,
                    RequestedOn = r.RequestedOn,
                    ReturnedAt = r.ReturnedAt,
                    FinalAt = r.FinalAt,
                    SaleReversedOn = r.SaleReversedOn,
                    StatusName = r.StatusName,
                    Reason = r.Reason,
                    IsCancelled = r.IsCancelled,
                },
            }).ToListAsync(ct);

        return new OrderFinanceDto
        {
            Accruals = accruals,
            Exemplars = exemplarIds.Select(id =>
            {
                var exemplarOrderIds = links.Where(l => l.ExemplarId == id).Select(l => l.OrderId).ToHashSet();
                return new OrderExemplarHistoryDto
                {
                    ExemplarId = id,
                    HasRestrictedHistory = exemplarOrderIds.Any(orderId => !visibleIdSet.Contains(orderId)),
                    Accruals = journal.Where(a => a.ExemplarId == id).ToList(),
                    Orders = orders.Where(o => exemplarOrderIds.Contains(o.Id)).ToList(),
                    Returns = returns.Where(r => r.ExemplarId == id).Select(r => r.Return).ToList(),
                };
            }).ToList(),
        };
    }
}
