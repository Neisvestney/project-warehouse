using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using ProjectWarehouse.Server.Data;
using ProjectWarehouse.Server.Domain;
using ProjectWarehouse.Server.Infrastructure;

namespace ProjectWarehouse.Server.Services;

public class UserQueryFilterService(ApplicationDbContext db) : IUserQueryFilterService
{
    public async Task<IQueryable<Warehouse>> GetWarehousesAsync(ClaimsPrincipal user, CancellationToken ct = default)
    {
        if (user.HasClaim("permission", Permissions.Warehouses.View))
            return db.Warehouses.AsQueryable();

        if (user.HasClaim("permission", Permissions.Warehouses.ViewAssigned))
        {
            var assignedIds = await GetAssignedWarehouseIdsAsync(user, ct);
            if (assignedIds != null)
                return db.Warehouses.Where(x => assignedIds.Contains(x.Id));
        }

        return db.Warehouses.Take(0);
    }

    public async Task<IQueryable<Receipt>> GetReceiptsAsync(ClaimsPrincipal user, CancellationToken ct = default)
    {
        var canViewAll = user.HasClaim("permission", Permissions.Receipts.View);
        var canViewAssigned = user.HasClaim("permission", Permissions.Receipts.ViewAssigned);
        var canProcess = user.HasClaim("permission", Permissions.Receipts.ProcessAssigned);

        if (canViewAll)
            return db.Receipts;

        if (canViewAssigned || canProcess)
        {
            var assignedIds = await GetAssignedWarehouseIdsAsync(user, ct);
            if (assignedIds != null)
            {
                return db.Receipts
                    .Where(x => assignedIds.Contains(x.WarehouseId))
                    .Where(x => canViewAssigned || (x.Status == ReceiptStatus.Processing && canProcess));
            }
        }

        return db.Receipts.Take(0);
    }

    public Task<IQueryable<ApplicationUser>> GetUsersAsync(ClaimsPrincipal user, CancellationToken ct = default)
    {
        IQueryable<ApplicationUser> result = user.HasClaim("permission", Permissions.Users.View)
            ? db.Users
            : db.Users.Take(0);

        return Task.FromResult(result);
    }

    public Task<IQueryable<CatalogItem>> GetCatalogItemsAsync(ClaimsPrincipal user, CancellationToken ct = default)
    {
        IQueryable<CatalogItem> result = user.HasClaim("permission", Permissions.Catalog.View)
            ? db.CatalogItems
            : db.CatalogItems.Take(0);

        return Task.FromResult(result);
    }

    public Task<IQueryable<MarketplaceAccount>> GetMarketplaceAccountsAsync(ClaimsPrincipal user, CancellationToken ct = default)
    {
        IQueryable<MarketplaceAccount> result = user.HasClaim("permission", Permissions.Integrations.View)
            ? db.MarketplaceAccounts
            : db.MarketplaceAccounts.Take(0);

        return Task.FromResult(result);
    }

    private async Task<HashSet<Guid>?> GetAssignedWarehouseIdsAsync(ClaimsPrincipal user, CancellationToken ct)
    {
        var raw = user.FindFirstValue(JwtRegisteredClaimNames.Sub);
        if (!Guid.TryParse(raw, out var userId)) return null;

        var ids = await db.Users
            .Where(u => u.Id == userId)
            .SelectMany(u => u.AssignedWarehouses)
            .Select(w => w.Id)
            .ToListAsync(ct);

        return [..ids];
    }
}
