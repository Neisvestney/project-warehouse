using System.Security.Claims;
using ProjectWarehouse.Server.Domain;
using ProjectWarehouse.Server.Infrastructure.Access;

namespace ProjectWarehouse.Server.Services;

/// <summary>
/// Row-level filters for lists, search and the calendar. Every method is the View predicate of the
/// entity's access rule — the same one that answers per-object checks in controllers.
/// </summary>
public class UserQueryFilterService(EntityAccessRegistry registry) : IUserQueryFilterService
{
    public Task<IQueryable<Warehouse>> GetWarehousesAsync(ClaimsPrincipal user, CancellationToken ct = default) =>
        View<Warehouse>(user, ct);

    public Task<IQueryable<Order>> GetOrdersAsync(ClaimsPrincipal user, CancellationToken ct = default) =>
        View<Order>(user, ct);

    public Task<IQueryable<Receipt>> GetReceiptsAsync(ClaimsPrincipal user, CancellationToken ct = default) =>
        View<Receipt>(user, ct);

    public Task<IQueryable<Stocktake>> GetStocktakesAsync(ClaimsPrincipal user, CancellationToken ct = default) =>
        View<Stocktake>(user, ct);

    public Task<IQueryable<ApplicationUser>> GetUsersAsync(ClaimsPrincipal user, CancellationToken ct = default) =>
        View<ApplicationUser>(user, ct);

    public Task<IQueryable<CatalogItem>> GetCatalogItemsAsync(ClaimsPrincipal user, CancellationToken ct = default) =>
        View<CatalogItem>(user, ct);

    public Task<IQueryable<MarketplaceAccount>> GetMarketplaceAccountsAsync(ClaimsPrincipal user, CancellationToken ct = default) =>
        View<MarketplaceAccount>(user, ct);

    public Task<IQueryable<StockMovement>> GetStockMovementsAsync(ClaimsPrincipal user, CancellationToken ct = default) =>
        View<StockMovement>(user, ct);

    private Task<IQueryable<T>> View<T>(ClaimsPrincipal user, CancellationToken ct) where T : class =>
        registry.For<T>().QueryAsync(user, AccessLevel.View, ct);
}
