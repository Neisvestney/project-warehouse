using System.Security.Claims;
using ProjectWarehouse.Server.Domain;

namespace ProjectWarehouse.Server.Services;

public interface IUserQueryFilterService
{
    Task<IQueryable<Warehouse>> GetWarehousesAsync(ClaimsPrincipal user, CancellationToken ct = default);
    Task<IQueryable<Receipt>> GetReceiptsAsync(ClaimsPrincipal user, CancellationToken ct = default);
    Task<IQueryable<Stocktake>> GetStocktakesAsync(ClaimsPrincipal user, CancellationToken ct = default);
    Task<IQueryable<ApplicationUser>> GetUsersAsync(ClaimsPrincipal user, CancellationToken ct = default);
    Task<IQueryable<CatalogItem>> GetCatalogItemsAsync(ClaimsPrincipal user, CancellationToken ct = default);
    Task<IQueryable<MarketplaceAccount>> GetMarketplaceAccountsAsync(ClaimsPrincipal user, CancellationToken ct = default);
    Task<IQueryable<StockMovement>> GetStockMovementsAsync(ClaimsPrincipal user, CancellationToken ct = default);
}
