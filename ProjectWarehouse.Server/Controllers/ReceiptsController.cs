using System.ComponentModel.DataAnnotations;
using AutoMapper;
using ValidationException = ProjectWarehouse.Server.Infrastructure.ValidationException;
using AutoMapper.QueryableExtensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProjectWarehouse.Server.Data;
using ProjectWarehouse.Server.Domain;
using ProjectWarehouse.Server.Infrastructure;
using ProjectWarehouse.Server.Infrastructure.ChangeLog;
using ProjectWarehouse.Server.Models;
using ProjectWarehouse.Server.Models.Receipts;
using ProjectWarehouse.Server.Services;

namespace ProjectWarehouse.Server.Controllers;

[Route("api/receipts")]
public class ReceiptsController(
    ApplicationDbContext db,
    IMapper mapper,
    IInventoryService inventory,
    IChangeLogService<ReceiptDto> changeLog) : AppControllerBase
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private IQueryable<Receipt> BaseQuery(bool includeItems = false)
    {
        var q = db.Receipts
            .Include(r => r.Warehouse)
            .AsQueryable();

        if (includeItems)
            q = q.Include(r => r.Items)
                .ThenInclude(i => i.CatalogItem)
                .Include(r => r.Items)
                .ThenInclude(i => i.Placements)
                .ThenInclude(p => p.StoragePlaceNode)
                .ThenInclude(n => n.RootStoragePlace)
                .Include(r => r.Items)
                .ThenInclude(i => i.Placements)
                .ThenInclude(p => p.UnitInventoryItem);

        return q;
    }

    private async Task<(bool canView, bool canViewAssigned, HashSet<Guid>? assignedIds, bool processingOnly)>
        GetViewAccessAsync(CancellationToken ct)
    {
        var canView         = User.HasClaim("permission", Permissions.Receipts.View);
        var canViewAssigned = User.HasClaim("permission", Permissions.Receipts.ViewAssigned);
        var canProcess      = User.HasClaim("permission", Permissions.Receipts.ProcessAssigned);

        if (!canView && !canViewAssigned && !canProcess)
            return (false, false, null, false);

        HashSet<Guid>? assignedIds = null;
        if (!canView)
            assignedIds = await GetCurrentUserAssignedWarehouseIdsAsync(db, ct);

        // processingOnly=true: user has ProcessAssigned but no broader view permission.
        // They may only see receipts in Processing status for their assigned warehouses.
        var processingOnly = canProcess && !canView && !canViewAssigned;

        return (canView, canViewAssigned, assignedIds, processingOnly);
    }

    private async Task<(bool canProcess, HashSet<Guid>? assignedIds)>
        GetProcessAccessAsync(CancellationToken ct)
    {
        if (!User.HasClaim("permission", Permissions.Receipts.ProcessAssigned))
            return (false, null);

        var assignedIds = await GetCurrentUserAssignedWarehouseIdsAsync(db, ct);
        return (true, assignedIds);
    }

    // ── GET list ──────────────────────────────────────────────────────────────

    /// <summary>List receipts with pagination, filtering, and search.</summary>
    [HttpGet]
    [Authorize]
    [ProducesResponseType<Paginated<ReceiptSummaryDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(
        [FromQuery][Range(1, int.MaxValue)] int page = 1,
        [FromQuery][Range(1, 200)] int pageSize = 20,
        [FromQuery] string? searchString = null,
        [FromQuery] Guid? warehouseId = null,
        [FromQuery] ReceiptStatus? status = null,
        [FromQuery] ReceiptReason? reason = null,
        [FromQuery] ReceiptSortBy sortBy = ReceiptSortBy.Number,
        [FromQuery] SortOrder sortOrder = SortOrder.Desc,
        CancellationToken ct = default)
    {
        var (canView, canViewAssigned, assignedIds, processingOnly) = await GetViewAccessAsync(ct);
        if (!canView && !canViewAssigned && !processingOnly)
            return Forbidden();

        // assignedIds is null when canView==true (not needed) or when the token is invalid.
        if (!canView && assignedIds is null)
            return Unauthorized(ErrorCode.TokenInvalid, "Invalid token.");

        var baseQuery = db.Receipts
            .Include(r => r.Warehouse)
            .Include(r => r.Items)
            .Where(r => warehouseId == null || r.WarehouseId == warehouseId)
            .Where(r => status == null || r.Status == status)
            .Where(r => reason == null || r.Reason == reason)
            .Where(r => assignedIds == null || assignedIds.Contains(r.WarehouseId))
            .Where(r => !processingOnly || r.Status == ReceiptStatus.Processing)
            .WhereMatchesSearch(r => r.SearchString, searchString);

        var query = sortBy switch
        {
            ReceiptSortBy.Status        => baseQuery.Sort(r => r.Status, sortOrder).ThenBy(r => r.Id),
            ReceiptSortBy.CreatedAt     => baseQuery.Sort(r => r.CreatedAt, sortOrder).ThenBy(r => r.Id),
            ReceiptSortBy.WarehouseName => baseQuery.Sort(r => r.Warehouse.Name, sortOrder).ThenBy(r => r.Id),
            ReceiptSortBy.Name          => baseQuery.Sort(r => r.Name, sortOrder).ThenBy(r => r.Id),
            _                           => baseQuery.Sort(r => r.Number, sortOrder).ThenBy(r => r.Id),
        };

        var paginated = await query
            .ProjectTo<ReceiptSummaryDto>(mapper.ConfigurationProvider)
            .ToPaginatedAsync(page, pageSize, ct);

        return Ok(paginated);
    }

    // ── GET single ────────────────────────────────────────────────────────────

    /// <summary>Get full receipt details including items and placements.</summary>
    [HttpGet("{id:guid}")]
    [Authorize]
    [ProducesResponseType<ReceiptDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct = default)
    {
        var (canView, canViewAssigned, assignedIds, processingOnly) = await GetViewAccessAsync(ct);
        if (!canView && !canViewAssigned && !processingOnly)
            return Forbidden();

        var receipt = await BaseQuery(includeItems: true)
            .FirstOrDefaultAsync(r => r.Id == id, ct);

        if (receipt is null)
            return NotFound(ErrorCode.ReceiptNotFound, "Receipt not found.");

        if (!canView && assignedIds is null)
            return Unauthorized(ErrorCode.TokenInvalid, "Invalid token.");

        if (assignedIds is not null && !assignedIds.Contains(receipt.WarehouseId))
            return Forbidden();

        if (processingOnly && receipt.Status != ReceiptStatus.Processing)
            return Forbidden();

        var nodeById = await LoadWarehouseNodesAsync(receipt.WarehouseId, ct);
        return Ok(mapper.Map<ReceiptDto>(receipt, opts => opts.Items["nodeById"] = nodeById));
    }

    // ── POST create ───────────────────────────────────────────────────────────

    /// <summary>Create a new receipt in Draft status.</summary>
    [HttpPost]
    [Authorize]
    [ProducesResponseType<ReceiptDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Create([FromBody] CreateReceiptRequest request, CancellationToken ct = default)
    {
        var canEdit         = User.HasClaim("permission", Permissions.Receipts.Edit);
        var canEditAssigned = User.HasClaim("permission", Permissions.Receipts.EditAssigned);

        if (!canEdit && !canEditAssigned)
            return Forbidden();

        var warehouse = await db.Warehouses.FindAsync([request.WarehouseId], ct);
        if (warehouse is null)
            return UnprocessableEntity("warehouseId", ErrorCode.WarehouseNotFound, "Warehouse not found.");

        if (canEditAssigned && !canEdit)
        {
            var assignedIds = await GetCurrentUserAssignedWarehouseIdsAsync(db, ct);
            if (assignedIds is null)
                return Unauthorized(ErrorCode.TokenInvalid, "Invalid token.");
            if (!assignedIds.Contains(request.WarehouseId))
                return Forbidden(ErrorCode.ReceiptNotAssignedToWarehouse,
                    "You are not assigned to the warehouse of this receipt.");
        }

        var receipt = new Receipt
        {
            Id                   = Guid.NewGuid(),
            Name                 = request.Name,
            Reason               = request.Reason,
            Notes                = request.Notes,
            PlannedDeliveryDate  = request.PlannedDeliveryDate,
            WarehouseId          = request.WarehouseId,
            CreatedById          = GetCurrentUserId(),
            CreatedAt            = DateTime.UtcNow,
            Status               = ReceiptStatus.Draft,
        };

        db.Receipts.Add(receipt);
        await db.SaveChangesAsync(ct);

        await db.Entry(receipt).Reference(r => r.Warehouse).LoadAsync(ct);

        var dto = mapper.Map<ReceiptDto>(receipt);
        await changeLog.CompareAndSaveToChangelog(null, dto);

        return CreatedAtAction(nameof(GetById), new { id = receipt.Id }, dto);
    }

    // ── PATCH update ──────────────────────────────────────────────────────────

    /// <summary>Update receipt name, reason, notes. Only allowed in Draft status.</summary>
    [HttpPatch("{id:guid}")]
    [Authorize]
    [ProducesResponseType<ReceiptDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateReceiptRequest request,
        CancellationToken ct = default)
    {
        var (receipt, error) = await LoadReceiptWithEditAccessAsync(id, ct);
        if (error is not null) return error;

        if (receipt!.Status != ReceiptStatus.Draft)
            return UnprocessableEntity("root", ErrorCode.ReceiptInvalidStatusTransition,
                "Receipt can only be updated in Draft status.");

        var before = mapper.Map<ReceiptDto>(receipt);

        receipt.Name                = request.Name;
        receipt.Reason              = request.Reason;
        receipt.Notes               = request.Notes;
        receipt.PlannedDeliveryDate = request.PlannedDeliveryDate;

        await db.SaveChangesAsync(ct);

        var after = mapper.Map<ReceiptDto>(receipt);
        await changeLog.CompareAndSaveToChangelog(before, after);

        return Ok(after);
    }

    // ── DELETE ────────────────────────────────────────────────────────────────

    /// <summary>Delete a receipt. Only allowed in Draft status.</summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = Permissions.Receipts.Edit)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct = default)
    {
        var receipt = await db.Receipts.FindAsync([id], ct);
        if (receipt is null)
            return NotFound(ErrorCode.ReceiptNotFound, "Receipt not found.");

        if (receipt.Status != ReceiptStatus.Draft)
            return UnprocessableEntity("root", ErrorCode.ReceiptInvalidStatusTransition,
                "Only Draft receipts can be deleted.");

        await db.Entry(receipt).Reference(r => r.Warehouse).LoadAsync(ct);
        var dto = mapper.Map<ReceiptDto>(receipt);

        db.Receipts.Remove(receipt);
        await db.SaveChangesAsync(ct);

        await changeLog.CompareAndSaveToChangelog(dto, null);

        return NoContent();
    }

    // ── PUT items sync ────────────────────────────────────────────────────────

    /// <summary>Replace the full list of expected items. Allowed in Draft and Planned statuses.</summary>
    [HttpPut("{id:guid}/items")]
    [Authorize]
    [ProducesResponseType<ReceiptDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> SyncItems(Guid id, [FromBody] IReadOnlyList<ReceiptItemRequest> items,
        CancellationToken ct = default)
    {
        var (receipt, error) = await LoadReceiptWithEditAccessAsync(id, ct, includeItems: true);
        if (error is not null) return error;

        if (receipt!.Status is not (ReceiptStatus.Draft or ReceiptStatus.Planned))
            return UnprocessableEntity("root", ErrorCode.ReceiptInvalidStatusTransition,
                "Items can only be modified in Draft or Planned status.");

        // #9: reject duplicate CatalogItemIds in the request
        var duplicates = items
            .GroupBy(x => x.CatalogItemId)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();
        if (duplicates.Count > 0)
            return UnprocessableEntity("root", ErrorCode.ValidationError,
                $"Duplicate catalog item(s) in request: {string.Join(", ", duplicates)}.");

        var before = mapper.Map<ReceiptDto>(receipt);

        var incomingIds = items.Select(x => x.CatalogItemId).ToHashSet();

        // Remove items no longer in the list
        var toRemove = receipt.Items.Where(i => !incomingIds.Contains(i.CatalogItemId)).ToList();
        foreach (var item in toRemove)
            db.ReceiptItems.Remove(item);

        // Update existing / add new
        foreach (var req in items)
        {
            var existing = receipt.Items.FirstOrDefault(i => i.CatalogItemId == req.CatalogItemId);
            if (existing is not null)
            {
                existing.PlannedCount = req.PlannedCount;
                existing.Notes        = req.Notes;
            }
            else
            {
                var catalogItem = await db.CatalogItems.FindAsync([req.CatalogItemId], ct);
                if (catalogItem is null)
                    return UnprocessableEntity("root", ErrorCode.CatalogItemNotFound,
                        $"Catalog item '{req.CatalogItemId}' not found.");

                db.ReceiptItems.Add(new ReceiptItem
                {
                    Id            = Guid.NewGuid(),
                    ReceiptId     = receipt.Id,
                    CatalogItemId = req.CatalogItemId,
                    CatalogItem   = catalogItem,
                    PlannedCount  = req.PlannedCount,
                    Notes         = req.Notes,
                });
            }
        }

        await db.SaveChangesAsync(ct);

        var after = mapper.Map<ReceiptDto>(receipt);
        await changeLog.CompareAndSaveToChangelog(before, after, ReceiptActions.ItemsSynced); // #10

        return Ok(after);
    }

    // ── PATCH received count ──────────────────────────────────────────────────

    /// <summary>Update the actually received count for a specific item. Only in Processing status.</summary>
    [HttpPatch("{id:guid}/items/{itemId:guid}/received-count")]
    [Authorize]
    [ProducesResponseType<ReceiptItemDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> UpdateReceivedCount(Guid id, Guid itemId,
        [FromBody] UpdateReceivedCountRequest request, CancellationToken ct = default)
    {
        var (receipt, processError) = await LoadReceiptWithProcessAccessAsync(id, ct);
        if (processError is not null) return processError;

        if (receipt!.Status != ReceiptStatus.Processing)
            return UnprocessableEntity("root", ErrorCode.ReceiptInvalidStatusTransition,
                "Received count can only be updated in Processing status.");

        var item = await db.ReceiptItems
            .Include(i => i.CatalogItem)
            .Include(i => i.Placements).ThenInclude(p => p.StoragePlaceNode).ThenInclude(n => n.RootStoragePlace)
            .Include(i => i.Placements).ThenInclude(p => p.UnitInventoryItem)
            .FirstOrDefaultAsync(i => i.Id == itemId && i.ReceiptId == id, ct);

        if (item is null)
            return NotFound(ErrorCode.ReceiptItemNotFound, "Receipt item not found.");

        var nodeById = await LoadWarehouseNodesAsync(receipt!.WarehouseId, ct);
        var itemBefore = mapper.Map<ReceiptItemDto>(item, opts => opts.Items["nodeById"] = nodeById);

        item.ReceivedCount = request.ReceivedCount;
        await db.SaveChangesAsync(ct);

        var itemAfter = mapper.Map<ReceiptItemDto>(item, opts => opts.Items["nodeById"] = nodeById);
        await changeLog.CompareAndSaveToChangelog(
            BuildItemChangelogSnapshot(receipt!, itemBefore),
            BuildItemChangelogSnapshot(receipt!, itemAfter),
            ReceiptActions.ReceivedCountUpdated);

        return Ok(itemAfter);
    }

    // ── POST placement / standard ─────────────────────────────────────────────

    /// <summary>Place Standard (count-based) items at a storage node. Only in Processing status.</summary>
    [HttpPost("{id:guid}/items/{itemId:guid}/placements/standard")]
    [Authorize]
    [ProducesResponseType<ReceiptItemDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> AddStandardPlacement(Guid id, Guid itemId,
        [FromBody] CreateStandardPlacementRequest request, CancellationToken ct = default)
    {
        var (receipt, item, error) = await LoadReceiptItemForPlacementAsync(id, itemId, ct);
        if (error is not null) return error;

        var nodeExists = await db.StoragePlacesNodes.AnyAsync(n => n.Id == request.StoragePlaceNodeId, ct);
        if (!nodeExists)
            return UnprocessableEntity("storagePlaceNodeId", ErrorCode.StoragePlaceNodeNotFound,
                "Storage place node not found.");

        var catalogItemId = item!.CatalogItemId;
        var warehouseId = receipt!.WarehouseId;

        var nodeById = await LoadWarehouseNodesAsync(warehouseId, ct);
        var itemBefore = mapper.Map<ReceiptItemDto>(item, opts => opts.Items["nodeById"] = nodeById);

        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);

            await inventory.AddStandardItemsToNodeAsync(
                request.StoragePlaceNodeId,
                catalogItemId,
                request.Count,
                ct: ct);

            db.ReceiptItemPlacements.Add(new ReceiptItemPlacement
            {
                Id                 = Guid.NewGuid(),
                ReceiptItemId      = itemId,
                StoragePlaceNodeId = request.StoragePlaceNodeId,
                Count              = request.Count,
            });
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        });

        var itemAfter = await LoadItemDtoAsync(itemId, warehouseId, ct, nodeById);
        await changeLog.CompareAndSaveToChangelog(
            BuildItemChangelogSnapshot(receipt!, itemBefore),
            BuildItemChangelogSnapshot(receipt!, itemAfter),
            ReceiptActions.PlacementAdded);

        return Ok(itemAfter);
    }

    // ── POST placement / unit ─────────────────────────────────────────────────

    /// <summary>Place a Unit (serialised) item at a storage node. Only in Processing status.</summary>
    [HttpPost("{id:guid}/items/{itemId:guid}/placements/unit")]
    [Authorize]
    [ProducesResponseType<ReceiptItemDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> AddUnitPlacement(Guid id, Guid itemId,
        [FromBody] CreateUnitPlacementRequest request, CancellationToken ct = default)
    {
        var (receipt, item, error) = await LoadReceiptItemForPlacementAsync(id, itemId, ct);
        if (error is not null) return error;

        var nodeExists = await db.StoragePlacesNodes.AnyAsync(n => n.Id == request.StoragePlaceNodeId, ct);
        if (!nodeExists)
            return UnprocessableEntity("storagePlaceNodeId", ErrorCode.StoragePlaceNodeNotFound,
                "Storage place node not found.");

        var catalogItemId = item!.CatalogItemId;
        var warehouseId = receipt!.WarehouseId;

        var nodeById = await LoadWarehouseNodesAsync(warehouseId, ct);
        var itemBefore = mapper.Map<ReceiptItemDto>(item, opts => opts.Items["nodeById"] = nodeById);

        var strategy = db.Database.CreateExecutionStrategy();
        try
        {
            await strategy.ExecuteAsync(async () =>
            {
                await using var tx = await db.Database.BeginTransactionAsync(ct);

                // Soft uniqueness check is done inside PlaceUnitItemToNodeAsync (via CreateUnitItemAsync).
                // DB unique constraint is the hard guard against races (caught as DbUpdateException below).
                var unitItem = await inventory.PlaceUnitItemToNodeAsync(
                    request.StoragePlaceNodeId,
                    catalogItemId,
                    request.UnitItem.InventoryNumber,
                    ct: ct);

                db.ReceiptItemPlacements.Add(new ReceiptItemPlacement
                {
                    Id                  = Guid.NewGuid(),
                    ReceiptItemId       = itemId,
                    StoragePlaceNodeId  = request.StoragePlaceNodeId,
                    Count               = 0,
                    UnitInventoryItemId = unitItem.Id,
                });
                await db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
            });
        }
        catch (ValidationException ex)
        {
            return UnprocessableEntity(ex);
        }
        catch (DbUpdateException)
        {
            // Race condition: soft check passed but DB unique constraint fired.
            return UnprocessableEntity("inventoryNumber", ErrorCode.UnitInventoryItemNumberDuplicate,
                "An item with this inventory number already exists for this catalog item.");
        }

        var itemAfter = await LoadItemDtoAsync(itemId, warehouseId, ct, nodeById);
        await changeLog.CompareAndSaveToChangelog(
            BuildItemChangelogSnapshot(receipt!, itemBefore),
            BuildItemChangelogSnapshot(receipt!, itemAfter),
            ReceiptActions.PlacementAdded);

        return Ok(itemAfter);
    }

    // ── POST placement / assembled-bundle ─────────────────────────────────────

    /// <summary>Place an AssembledBundle item at a storage node. Only in Processing status.</summary>
    [HttpPost("{id:guid}/items/{itemId:guid}/placements/assembled-bundle")]
    [Authorize]
    [ProducesResponseType<ReceiptItemDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> AddAssembledBundlePlacement(Guid id, Guid itemId,
        [FromBody] CreateAssembledBundlePlacementRequest request, CancellationToken ct = default)
    {
        var (receipt, item, error) = await LoadReceiptItemForPlacementAsync(id, itemId, ct);
        if (error is not null) return error;

        var nodeExists = await db.StoragePlacesNodes.AnyAsync(n => n.Id == request.StoragePlaceNodeId, ct);
        if (!nodeExists)
            return UnprocessableEntity("storagePlaceNodeId", ErrorCode.StoragePlaceNodeNotFound,
                "Storage place node not found.");

        var catalogItemId = item!.CatalogItemId;
        var warehouseId = receipt!.WarehouseId;

        // Validate that the request components exactly match the catalog item's AssembledComponents definition.
        var definedComponents = await db.CatalogItems
            .Where(c => c.Id == catalogItemId)
            .SelectMany(c => c.AssembledComponents)
            .Include(ac => ac.Component)
            .ToListAsync(ct);

        var validationMessages = new List<string>();

        foreach (var defined in definedComponents)
        {
            var isUnit = defined.Component.Type == CatalogItemType.Unit;
            var matching = request.Components.Where(c => c.CatalogItemId == defined.ComponentId).ToList();

            if (isUnit)
            {
                if (matching.Count != defined.Quantity)
                    validationMessages.Add(
                        $"«{defined.Component.Name}»: ожидается {defined.Quantity} шт., получено {matching.Count}.");
            }
            else
            {
                if (matching.Count != 1)
                    validationMessages.Add(
                        $"«{defined.Component.Name}»: ожидается 1 запись, получено {matching.Count}.");
                else if (matching[0].Quantity != defined.Quantity)
                    validationMessages.Add(
                        $"«{defined.Component.Name}»: ожидается количество {defined.Quantity}, получено {matching[0].Quantity}.");
            }
        }

        var definedIds = definedComponents.Select(c => c.ComponentId).ToHashSet();
        var extraIds = request.Components
            .Select(c => c.CatalogItemId)
            .Where(cid => !definedIds.Contains(cid))
            .Distinct()
            .ToList();
        if (extraIds.Count > 0)
            validationMessages.Add($"Лишние компоненты не входят в состав комплекта.");

        if (validationMessages.Count > 0)
            return UnprocessableEntity("root", ErrorCode.ValidationError,
                string.Join(" ", validationMessages));

        var nodeById = await LoadWarehouseNodesAsync(warehouseId, ct);
        var itemBefore = mapper.Map<ReceiptItemDto>(item, opts => opts.Items["nodeById"] = nodeById);

        var strategy = db.Database.CreateExecutionStrategy();
        try
        {
            await strategy.ExecuteAsync(async () =>
            {
                await using var tx = await db.Database.BeginTransactionAsync(ct);

                var bundleItem = await inventory.AddAssembledBundleToNodeAsync(
                    request.StoragePlaceNodeId,
                    catalogItemId,
                    request.Components,
                    ct: ct);

                db.ReceiptItemPlacements.Add(new ReceiptItemPlacement
                {
                    Id                             = Guid.NewGuid(),
                    ReceiptItemId                  = itemId,
                    StoragePlaceNodeId             = request.StoragePlaceNodeId,
                    Count                          = 0,
                    AssembledBundleInventoryItemId = bundleItem.Id,
                });
                await db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
            });
        }
        catch (ValidationException ex)
        {
            // Field already contains the full path with component index, e.g. "components[0].inventoryNumber".
            return UnprocessableEntity(ex);
        }
        catch (DbUpdateException)
        {
            // Race condition: a new unit item component hit the DB unique constraint.
            return UnprocessableEntity("components", ErrorCode.UnitInventoryItemNumberDuplicate,
                "One or more unit item inventory numbers already exist for their catalog items.");
        }

        var itemAfter = await LoadItemDtoAsync(itemId, warehouseId, ct, nodeById);
        await changeLog.CompareAndSaveToChangelog(
            BuildItemChangelogSnapshot(receipt!, itemBefore),
            BuildItemChangelogSnapshot(receipt!, itemAfter),
            ReceiptActions.PlacementAdded);

        return Ok(itemAfter);
    }

    // ── DELETE placement ──────────────────────────────────────────────────────

    /// <summary>Remove a placement, reversing the inventory change. Only in Processing status.</summary>
    [HttpDelete("{id:guid}/items/{itemId:guid}/placements/{placementId:guid}")]
    [Authorize]
    [ProducesResponseType<ReceiptItemDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> DeletePlacement(Guid id, Guid itemId, Guid placementId,
        CancellationToken ct = default)
    {
        var (receipt, processError) = await LoadReceiptWithProcessAccessAsync(id, ct);
        if (processError is not null) return processError;

        if (receipt!.Status != ReceiptStatus.Processing)
            return UnprocessableEntity("root", ErrorCode.ReceiptInvalidStatusTransition,
                "Placements can only be removed during Processing status.");

        var placement = await db.ReceiptItemPlacements
            .FirstOrDefaultAsync(p => p.Id == placementId && p.ReceiptItemId == itemId, ct);

        if (placement is null)
            return NotFound(ErrorCode.ReceiptItemPlacementNotFound, "Placement not found.");

        // The item is already in receipt.Items (loaded by BaseQuery with includeItems: true).
        // Use it to build the before snapshot; for standard placements also grab CatalogItemId.
        var itemEntity = receipt!.Items.First(i => i.Id == itemId);

        var nodeById = await LoadWarehouseNodesAsync(receipt.WarehouseId, ct);
        var itemBefore = mapper.Map<ReceiptItemDto>(itemEntity, opts => opts.Items["nodeById"] = nodeById);

        // Reverse the inventory change and remove the placement record atomically.
        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);

            if (placement.UnitInventoryItemId is not null)
                await inventory.RemoveUnitItemAsync(placement.UnitInventoryItemId.Value, ct: ct);
            else if (placement.AssembledBundleInventoryItemId is not null)
                await inventory.RemoveAssembledBundleAsync(placement.AssembledBundleInventoryItemId.Value, ct: ct);
            else if (placement.Count > 0)
                await inventory.RemoveStandardItemsFromNodeAsync(
                    placement.StoragePlaceNodeId,
                    itemEntity.CatalogItemId,
                    placement.Count,
                    ct: ct);

            db.ReceiptItemPlacements.Remove(placement);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        });

        var itemAfter = await LoadItemDtoAsync(itemId, receipt.WarehouseId, ct, nodeById);
        await changeLog.CompareAndSaveToChangelog(
            BuildItemChangelogSnapshot(receipt, itemBefore),
            BuildItemChangelogSnapshot(receipt, itemAfter),
            ReceiptActions.PlacementRemoved);

        return Ok(itemAfter);
    }

    // ── Status transitions ────────────────────────────────────────────────────

    /// <summary>Transition: Draft → Planned.</summary>
    [HttpPost("{id:guid}/plan")]
    [Authorize]
    [ProducesResponseType<ReceiptDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public Task<IActionResult> Plan(Guid id, CancellationToken ct = default) =>
        TransitionAsync(id, ReceiptStatus.Draft, ReceiptStatus.Planned, ReceiptActions.Planned, ct);

    /// <summary>Transition: Planned → Processing.</summary>
    [HttpPost("{id:guid}/start-processing")]
    [Authorize]
    [ProducesResponseType<ReceiptDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public Task<IActionResult> StartProcessing(Guid id, CancellationToken ct = default) =>
        TransitionAsync(id, ReceiptStatus.Planned, ReceiptStatus.Processing, ReceiptActions.ProcessingStarted, ct);

    /// <summary>Transition: Processing → Finished. Validates that each item with a received count has enough placements.</summary>
    [HttpPost("{id:guid}/finish")]
    [Authorize]
    [ProducesResponseType<ReceiptDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Finish(Guid id, CancellationToken ct = default)
    {
        var (receipt, error) = await LoadReceiptWithEditAccessAsync(id, ct, includeItems: true);
        if (error is not null) return error;

        if (receipt!.Status != ReceiptStatus.Processing)
            return UnprocessableEntity("root", ErrorCode.ReceiptInvalidStatusTransition,
                $"Receipt must be in 'Processing' status to finish (current: '{receipt.Status}').");

        var underplaced = receipt.Items
            .Where(i => i.ReceivedCount.HasValue)
            .Where(i =>
            {
                var placed = i.Placements.Sum(p => p.Count == 0 ? 1 : p.Count);
                return placed < i.ReceivedCount!.Value;
            })
            .ToList();

        if (underplaced.Count > 0)
            return UnprocessableEntity("root", ErrorCode.ReceiptItemsUnderplaced,
                $"Некоторые позиции размещены не полностью: {string.Join(", ", underplaced.Select(i => i.CatalogItem?.Name ?? i.Id.ToString()))}.");

        var overplaced = receipt.Items
            .Where(i => i.ReceivedCount.HasValue)
            .Where(i =>
            {
                var placed = i.Placements.Sum(p => p.Count == 0 ? 1 : p.Count);
                return placed > i.ReceivedCount!.Value;
            })
            .ToList();

        if (overplaced.Count > 0)
            return UnprocessableEntity("root", ErrorCode.ReceiptItemsOverplaced,
                $"Некоторые позиции размещены сверх принятого количества: {string.Join(", ", overplaced.Select(i => i.CatalogItem?.Name ?? i.Id.ToString()))}.");

        var before = mapper.Map<ReceiptDto>(receipt);
        receipt.Status = ReceiptStatus.Finished;
        await db.SaveChangesAsync(ct);

        var nodeById = await LoadWarehouseNodesAsync(receipt.WarehouseId, ct);
        var after = mapper.Map<ReceiptDto>(receipt, opts => opts.Items["nodeById"] = nodeById);
        await changeLog.CompareAndSaveToChangelog(before, after, ReceiptActions.Finished);

        return Ok(after);
    }

    /// <summary>Revert one step back (Planned → Draft, Processing → Planned if no placements).</summary>
    [HttpPost("{id:guid}/revert")]
    [Authorize]
    [ProducesResponseType<ReceiptDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Revert(Guid id, CancellationToken ct = default)
    {
        var (receipt, error) = await LoadReceiptWithEditAccessAsync(id, ct, includeItems: true);
        if (error is not null) return error;

        var before = mapper.Map<ReceiptDto>(receipt!);

        ReceiptStatus nextStatus;
        switch (receipt!.Status)
        {
            case ReceiptStatus.Planned:
                nextStatus = ReceiptStatus.Draft;
                break;

            case ReceiptStatus.Processing:
                if (receipt.Items.Any(i => i.Placements.Count > 0))
                    return UnprocessableEntity("root", ErrorCode.ReceiptHasPlacements,
                        "Cannot revert from Processing: some items already have placements. Remove them first.");
                nextStatus = ReceiptStatus.Planned;
                break;

            case ReceiptStatus.Finished:
                nextStatus = ReceiptStatus.Processing;
                break;

            default:
                return UnprocessableEntity("root", ErrorCode.ReceiptInvalidStatusTransition,
                    $"Cannot revert from '{receipt.Status}' status.");
        }

        receipt.Status = nextStatus;
        await db.SaveChangesAsync(ct);

        var nodeById = await LoadWarehouseNodesAsync(receipt.WarehouseId, ct);
        var after = mapper.Map<ReceiptDto>(receipt, opts => opts.Items["nodeById"] = nodeById);
        await changeLog.CompareAndSaveToChangelog(before, after, ReceiptActions.Reverted);

        return Ok(after);
    }

    /// <summary>Cancel the receipt. Allowed from Draft, Planned, and Processing (if no placements).</summary>
    [HttpPost("{id:guid}/cancel")]
    [Authorize]
    [ProducesResponseType<ReceiptDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken ct = default)
    {
        var (receipt, error) = await LoadReceiptWithEditAccessAsync(id, ct, includeItems: true);
        if (error is not null) return error;

        if (receipt!.Status is ReceiptStatus.Finished or ReceiptStatus.Canceled)
            return UnprocessableEntity("root", ErrorCode.ReceiptInvalidStatusTransition,
                $"Cannot cancel a receipt in '{receipt.Status}' status.");

        if (receipt.Status == ReceiptStatus.Processing &&
            receipt.Items.Any(i => i.Placements.Count > 0))
            return UnprocessableEntity("root", ErrorCode.ReceiptHasPlacements,
                "Cannot cancel: some items already have placements. Remove them first.");

        var before = mapper.Map<ReceiptDto>(receipt);
        receipt.Status = ReceiptStatus.Canceled;
        await db.SaveChangesAsync(ct);

        var nodeById = await LoadWarehouseNodesAsync(receipt.WarehouseId, ct);
        var after = mapper.Map<ReceiptDto>(receipt, opts => opts.Items["nodeById"] = nodeById);
        await changeLog.CompareAndSaveToChangelog(before, after, ReceiptActions.Canceled);

        return Ok(after);
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private async Task<IActionResult> TransitionAsync(
        Guid id, ReceiptStatus from, ReceiptStatus to, string action, CancellationToken ct)
    {
        var (receipt, error) = await LoadReceiptWithEditAccessAsync(id, ct, includeItems: true);
        if (error is not null) return error;

        if (receipt!.Status != from)
            return UnprocessableEntity("root", ErrorCode.ReceiptInvalidStatusTransition,
                $"Receipt must be in '{from}' status to perform this action (current: '{receipt.Status}').");

        var before = mapper.Map<ReceiptDto>(receipt);
        receipt.Status = to;
        await db.SaveChangesAsync(ct);

        var nodeById = await LoadWarehouseNodesAsync(receipt.WarehouseId, ct);
        var after = mapper.Map<ReceiptDto>(receipt, opts => opts.Items["nodeById"] = nodeById);
        await changeLog.CompareAndSaveToChangelog(before, after, action);

        return Ok(after);
    }

    private async Task<(Receipt? receipt, IActionResult? error)> LoadReceiptWithEditAccessAsync(
        Guid id, CancellationToken ct, bool includeItems = false)
    {
        var canEdit         = User.HasClaim("permission", Permissions.Receipts.Edit);
        var canEditAssigned = User.HasClaim("permission", Permissions.Receipts.EditAssigned);

        if (!canEdit && !canEditAssigned)
            return (null, Forbidden());

        var receipt = await BaseQuery(includeItems).FirstOrDefaultAsync(r => r.Id == id, ct);
        if (receipt is null)
            return (null, NotFound(ErrorCode.ReceiptNotFound, "Receipt not found."));

        if (canEditAssigned && !canEdit)
        {
            var assignedIds = await GetCurrentUserAssignedWarehouseIdsAsync(db, ct);
            if (assignedIds is null)
                return (null, Unauthorized(ErrorCode.TokenInvalid, "Invalid token."));
            if (!assignedIds.Contains(receipt.WarehouseId))
                return (null, Forbidden(ErrorCode.ReceiptNotAssignedToWarehouse,
                    "You are not assigned to the warehouse of this receipt."));
        }

        return (receipt, null);
    }

    private async Task<(Receipt? receipt, IActionResult? error)> LoadReceiptWithProcessAccessAsync(
        Guid id, CancellationToken ct)
    {
        // receipts.edit = full admin, can process any receipt without warehouse restriction.
        // receipts.process_assigned = operator, restricted to assigned warehouses only.
        var canEdit    = User.HasClaim("permission", Permissions.Receipts.Edit);
        var (canProcess, assignedIds) = await GetProcessAccessAsync(ct);

        if (!canEdit && !canProcess)
            return (null, Forbidden());

        // Only operators (process_assigned without edit) need warehouse assignment check.
        if (!canEdit && assignedIds is null)
            return (null, Unauthorized(ErrorCode.TokenInvalid, "Invalid token."));

        var receipt = await BaseQuery(includeItems: true).FirstOrDefaultAsync(r => r.Id == id, ct);
        if (receipt is null)
            return (null, NotFound(ErrorCode.ReceiptNotFound, "Receipt not found."));

        if (!canEdit && assignedIds is not null && !assignedIds.Contains(receipt.WarehouseId))
            return (null, Forbidden(ErrorCode.ReceiptNotAssignedToWarehouse,
                "You are not assigned to the warehouse of this receipt."));

        return (receipt, null);
    }

    private async Task<(Receipt? receipt, ReceiptItem? item, IActionResult? error)>
        LoadReceiptItemForPlacementAsync(Guid receiptId, Guid itemId, CancellationToken ct)
    {
        var (receipt, error) = await LoadReceiptWithProcessAccessAsync(receiptId, ct);
        if (error is not null)
            return (null, null, error);

        if (receipt!.Status != ReceiptStatus.Processing)
            return (null, null, UnprocessableEntity("root", ErrorCode.ReceiptInvalidStatusTransition,
                "Placements can only be added during Processing status."));

        var item = receipt.Items.FirstOrDefault(i => i.Id == itemId);
        if (item is null)
            return (null, null, NotFound(ErrorCode.ReceiptItemNotFound, "Receipt item not found."));

        return (receipt, item, null);
    }

    private async Task<Dictionary<Guid, StoragePlaceNode>> LoadWarehouseNodesAsync(
        Guid warehouseId, CancellationToken ct) =>
        await db.StoragePlacesNodes
            .Where(n => n.RootStoragePlace.WarehouseId == warehouseId)
            .Include(n => n.RootStoragePlace)
            .ToDictionaryAsync(n => n.Id, ct);

    private async Task<ReceiptItemDto> LoadItemDtoAsync(
        Guid itemId, Guid warehouseId, CancellationToken ct,
        Dictionary<Guid, StoragePlaceNode>? nodeById = null)
    {
        nodeById ??= await LoadWarehouseNodesAsync(warehouseId, ct);
        var item = await db.ReceiptItems
            .Include(i => i.CatalogItem)
            .Include(i => i.Placements).ThenInclude(p => p.StoragePlaceNode).ThenInclude(n => n.RootStoragePlace)
            .Include(i => i.Placements).ThenInclude(p => p.UnitInventoryItem)
            .FirstAsync(i => i.Id == itemId, ct);
        return mapper.Map<ReceiptItemDto>(item, opts => opts.Items["nodeById"] = nodeById);
    }

    /// <summary>
    /// Builds a lightweight <see cref="ReceiptDto"/> snapshot containing only the given item.
    /// Used for changelog diffs on item-level operations (received count, placements).
    /// </summary>
    private ReceiptDto BuildItemChangelogSnapshot(Receipt receipt, ReceiptItemDto itemDto) =>
        new()
        {
            Id                  = receipt.Id,
            Number              = receipt.Number,
            Name                = receipt.Name,
            Reason              = receipt.Reason,
            Status              = receipt.Status,
            Notes               = receipt.Notes,
            PlannedDeliveryDate = receipt.PlannedDeliveryDate,
            CreatedAt           = receipt.CreatedAt,
            WarehouseId         = receipt.WarehouseId,
            WarehouseName       = receipt.Warehouse.Name,
            TotalPlannedCount   = itemDto.PlannedCount,
            TotalReceivedCount  = itemDto.ReceivedCount ?? 0,
            Items               = [itemDto],
        };
}
