using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProjectWarehouse.Server.Data;
using ProjectWarehouse.Server.Domain;
using ProjectWarehouse.Server.Infrastructure;
using ProjectWarehouse.Server.Infrastructure.Access;
using ProjectWarehouse.Server.Models.Orders;
using ProjectWarehouse.Server.Services;

namespace ProjectWarehouse.Server.Controllers;

[Route("api/orders")]
public class OrderFinanceController(
    ApplicationDbContext db,
    EntityAccessRegistry access,
    IOrderFinanceService finance) : AppControllerBase
{
    /// <summary>Order accrual journal and Wildberries exemplar history.</summary>
    /// <remarks>
    /// Requires analytics.view and orders.view or orders.view_assigned, with access to this order.
    /// Accruals include fees without a matched item. Exemplar history is limited to the same account,
    /// includes unlinked journal rows and returns, and excludes inaccessible related orders and their rows.
    /// HasRestrictedHistory indicates that the returned exemplar history omits inaccessible records.
    /// Dates and nullable event times are returned as stored; currencies are never combined.
    /// Returns 404 orderNotFound when the order does not exist.
    /// </remarks>
    [HttpGet("{id:guid}/finance")]
    [Authorize(Policy = Permissions.Analytics.View)]
    [ProducesResponseType<OrderFinanceDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct = default)
    {
        if (!AccessScope.Has(User, Permissions.Orders.View) &&
            !AccessScope.Has(User, Permissions.Orders.ViewAssigned))
            return Forbidden();

        var rule = access.For<Order>();
        if (AccessError(await rule.PrecheckAsync(User, AccessLevel.View, ct)) is { } prelude)
            return prelude;

        var order = await db.Orders.AsNoTracking()
            .Include(o => o.MarketplaceOrder).ThenInclude(m => m!.MarketplaceAccount)
            .SingleOrDefaultAsync(o => o.Id == id, ct);
        if (order is null)
            return NotFound(ErrorCode.OrderNotFound, "Order not found.");
        if (AccessError(await rule.CheckAsync(User, AccessLevel.View, order, ct)) is { } denied)
            return denied;

        return Ok(await finance.GetAsync(User, order, ct));
    }
}
