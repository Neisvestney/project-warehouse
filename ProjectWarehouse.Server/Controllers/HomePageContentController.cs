using AutoMapper;
using AutoMapper.QueryableExtensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProjectWarehouse.Server.Data;
using ProjectWarehouse.Server.Domain;
using ProjectWarehouse.Server.Infrastructure;
using ProjectWarehouse.Server.Models;

namespace ProjectWarehouse.Server.Controllers;

[Route("api/homepagecontent")]
public class HomePageContentController(ApplicationDbContext db, IMapper mapper) : AppControllerBase
{
    /// <summary>Get list of AppEntities for home page.</summary>
    [HttpGet]
    [Authorize]
    [ProducesResponseType<IReadOnlyList<AppEntity>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetHomePageContent(CancellationToken ct = default)
    {
        var list = new List<AppEntity>();

        var userCanViewAllWarehouses = User.HasClaim("permission", Permissions.Warehouses.View);
        var userCanViewAssignedWarehouses = User.HasClaim("permission", Permissions.Warehouses.ViewAssigned);

        if (userCanViewAllWarehouses)
        {
            var warehouses = await db.Warehouses.ProjectTo<AppEntity>(mapper.ConfigurationProvider).Take(2).ToListAsync();
            list.AddRange(warehouses);
        }
        else if (userCanViewAssignedWarehouses)
        {
            var assignedIds = await GetCurrentUserAssignedWarehouseIdsAsync(db, ct);
            if (assignedIds != null)
            {
                var warehouses = await db.Warehouses
                    .Where(x => assignedIds.Contains(x.Id))
                    .ProjectTo<AppEntity>(mapper.ConfigurationProvider)
                    .Take(2)
                    .ToListAsync();
                list.AddRange(warehouses);
            }
        }

        // Draft receipts created by the current user
        var currentUserId = GetCurrentUserId();
        if (currentUserId.HasValue)
        {
            var draftReceipts = await db.Receipts
                .Where(r => r.CreatedById == currentUserId.Value && r.Status == ReceiptStatus.Draft)
                .ProjectTo<AppEntity>(mapper.ConfigurationProvider)
                .ToListAsync(ct);
            list.AddRange(draftReceipts);
        }

        // Processing receipts accessible to the current user
        var canViewAll      = User.HasClaim("permission", Permissions.Receipts.View);
        var canViewAssigned = User.HasClaim("permission", Permissions.Receipts.ViewAssigned);
        var canProcess      = User.HasClaim("permission", Permissions.Receipts.ProcessAssigned);

        if (canViewAll || canViewAssigned || canProcess)
        {
            HashSet<Guid>? receiptWarehouseIds = null;
            if (!canViewAll)
            {
                receiptWarehouseIds = await GetCurrentUserAssignedWarehouseIdsAsync(db, ct);
                if (receiptWarehouseIds == null)
                    return Ok(list);
            }

            var processingReceipts = await db.Receipts
                .Where(r => r.Status == ReceiptStatus.Processing)
                .Where(r => receiptWarehouseIds == null || receiptWarehouseIds.Contains(r.WarehouseId))
                .ProjectTo<AppEntity>(mapper.ConfigurationProvider)
                .ToListAsync(ct);
            list.AddRange(processingReceipts);
        }

        return Ok(list);
    }
}