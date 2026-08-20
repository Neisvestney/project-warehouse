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
using ProjectWarehouse.Server.Infrastructure.Access;
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
    EntityAccessRegistry access,
    AccessScope scope,
    IChangeLogService<ReceiptDto> changeLog) : AppControllerBase
{
    private EntityAccessRule<Receipt> Rule => access.For<Receipt>();

    // ── Helpers ───────────────────────────────────────────────────────────────

    private IQueryable<Receipt> BaseQuery(bool includeItems = false)
    {
        var q = db.Receipts
            .Include(r => r.Warehouse)
            .AsQueryable();

        if (includeItems)
            q = q.Include(r => r.Items)
                .ThenInclude(i => i.CatalogItem).ThenInclude(c => c.Group)
                .Include(r => r.Items)
                .ThenInclude(i => i.Placements)
                .ThenInclude(p => p.StoragePlaceNode)
                .ThenInclude(n => n.RootStoragePlace)
                .Include(r => r.Items)
                .ThenInclude(i => i.Placements)
                .ThenInclude(p => p.UnitInventoryItem);

        return q;
    }

    private async Task<(bool canProcess, HashSet<Guid>? assignedIds)>
        GetProcessAccessAsync(CancellationToken ct)
    {
        if (!AccessScope.Has(User, Permissions.Receipts.ProcessAssigned))
            return (false, null);

        var assignedIds = await scope.GetAssignedWarehouseIdsAsync(User, ct);
        return (true, assignedIds);
    }

    // ── GET list ──────────────────────────────────────────────────────────────

    /// <summary>List receipts with pagination, filtering, and search.</summary>
    /// <remarks>
    /// Query params: <c>page</c> (default 1), <c>pageSize</c> (default 20, max 200), <c>searchString</c>,
    /// <c>warehouseId</c>, <c>status</c>, <c>reason</c>, <c>sortBy</c> (default <c>Number</c>),
    /// <c>sortOrder</c> (default <c>Desc</c>).
    /// Requires <c>receipts.view</c> or <c>receipts.view_assigned</c>; <c>receipts.process_assigned</c> alone
    /// also opens the list but narrows it to receipts in <c>Processing</c> status. Without any of them, 403
    /// <c>permissionDenied</c>; 401 <c>tokenInvalid</c> when an <c>_assigned</c> permission is used but the
    /// token carries no resolvable user.
    /// </remarks>
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
        if (AccessError(await Rule.PrecheckAsync(User, AccessLevel.View, ct)) is { } error)
            return error;

        var accessible = await Rule.QueryAsync(User, AccessLevel.View, ct);

        var baseQuery = accessible
            .Include(r => r.Warehouse)
            .Include(r => r.Items)
            .Where(r => warehouseId == null || r.WarehouseId == warehouseId)
            .Where(r => status == null || r.Status == status)
            .Where(r => reason == null || r.Reason == reason)
            .WhereMatchesSearch(r => r.SearchString, searchString);

        var query = sortBy switch
        {
            ReceiptSortBy.Status              => baseQuery.Sort(r => r.Status, sortOrder).ThenBy(r => r.Id),
            ReceiptSortBy.CreatedAt           => baseQuery.Sort(r => r.CreatedAt, sortOrder).ThenBy(r => r.Id),
            ReceiptSortBy.WarehouseName       => baseQuery.Sort(r => r.Warehouse.Name, sortOrder).ThenBy(r => r.Id),
            ReceiptSortBy.Name                => baseQuery.Sort(r => r.Name, sortOrder).ThenBy(r => r.Id),
            ReceiptSortBy.PlannedDeliveryDate => baseQuery.Sort(r => r.PlannedDeliveryDate, sortOrder).ThenBy(r => r.Id),
            _                                 => baseQuery.Sort(r => r.Number, sortOrder).ThenBy(r => r.Id),
        };

        var paginated = await query
            .ProjectTo<ReceiptSummaryDto>(mapper.ConfigurationProvider)
            .ToPaginatedAsync(page, pageSize, ct);

        return Ok(paginated);
    }

    // ── GET single ────────────────────────────────────────────────────────────

    /// <summary>Get full receipt details including items and placements.</summary>
    /// <remarks>
    /// Errors: 404 <c>receiptNotFound</c>; 403 <c>receiptNotAssignedToWarehouse</c> when only an
    /// <c>_assigned</c> permission is held and the receipt belongs to another warehouse; 403
    /// <c>permissionDenied</c> without a view permission, or when <c>receipts.process_assigned</c> is the only
    /// grant and the receipt is not in <c>Processing</c>; 401 <c>tokenInvalid</c> for an unresolvable user.
    /// </remarks>
    [HttpGet("{id:guid}")]
    [Authorize]
    [ProducesResponseType<ReceiptDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct = default)
    {
        if (AccessError(await Rule.PrecheckAsync(User, AccessLevel.View, ct)) is { } prelude)
            return prelude;

        var receipt = await BaseQuery(includeItems: true)
            .FirstOrDefaultAsync(r => r.Id == id, ct);

        if (receipt is null)
            return NotFound(ErrorCode.ReceiptNotFound, "Receipt not found.");

        if (AccessError(await Rule.CheckAsync(User, AccessLevel.View, receipt, ct)) is { } denied)
            return denied;

        var nodeById = await LoadWarehouseNodesAsync(receipt.WarehouseId, ct);
        return Ok(mapper.Map<ReceiptDto>(receipt, opts => opts.Items["nodeById"] = nodeById));
    }

    // ── POST create ───────────────────────────────────────────────────────────

    /// <summary>Create a new receipt in Draft status.</summary>
    /// <remarks>
    /// Body: <c>CreateReceiptRequest</c> — warehouseId (required), name, reason, notes, plannedDeliveryDate.
    /// Errors: 422 <c>warehouseNotFound</c> for an unknown warehouse; 403 <c>permissionDenied</c> without
    /// <c>receipts.edit</c>/<c>receipts.edit_assigned</c>, or 403 <c>receiptNotAssignedToWarehouse</c> when
    /// only <c>receipts.edit_assigned</c> is held and the target warehouse is not assigned.
    /// </remarks>
    [HttpPost]
    [Authorize]
    [ProducesResponseType<ReceiptDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Create([FromBody] CreateReceiptRequest request, CancellationToken ct = default)
    {
        if (AccessError(await Rule.PrecheckAsync(User, AccessLevel.Edit, ct)) is { } error)
            return error;

        var warehouse = await db.Warehouses.FindAsync([request.WarehouseId], ct);
        if (warehouse is null)
            return UnprocessableEntity("warehouseId", ErrorCode.WarehouseNotFound, "Warehouse not found.");

        if (AccessError(await Rule.CheckWarehouseAsync(User, AccessLevel.Edit, request.WarehouseId, ct)) is { } denied)
            return denied;

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
    /// <remarks>
    /// Errors: 404 <c>receiptNotFound</c>; 422 <c>receiptInvalidStatusTransition</c> outside Draft status;
    /// 403 <c>permissionDenied</c> / <c>receiptNotAssignedToWarehouse</c> (edit access).
    /// </remarks>
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
    /// <remarks>
    /// Requires the full <c>receipts.edit</c> permission — <c>receipts.edit_assigned</c> does not delete.
    /// Errors: 404 <c>receiptNotFound</c>; 422 <c>receiptInvalidStatusTransition</c> outside Draft status.
    /// </remarks>
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

    // ── POST items / quick-add ────────────────────────────────────────────────

    /// <summary>
    /// Add a single catalog item to the receipt with plannedCount=0 during Processing.
    /// Used when a new item is discovered while physically receiving goods.
    /// </summary>
    /// <remarks>
    /// Processing status only. Requires <c>receipts.edit</c> (any warehouse) or
    /// <c>receipts.process_assigned</c> (assigned warehouses only). Errors:
    /// <list type="bullet">
    ///   <item>404 <c>receiptNotFound</c></item>
    ///   <item>422 <c>receiptInvalidStatusTransition</c> — receipt is not in Processing</item>
    ///   <item>422 <c>catalogItemNotFound</c> — unknown catalog item</item>
    ///   <item>422 <c>catalogItemIsImmutable</c> — the catalog item is archived</item>
    ///   <item>422 <c>validationError</c> — the item is a ProductGroup, Variation or Bundle, or it is already
    ///     in the receipt</item>
    ///   <item>403 <c>permissionDenied</c> (neither permission), 403 <c>receiptNotAssignedToWarehouse</c>
    ///     (operator, other warehouse), 401 <c>tokenInvalid</c></item>
    /// </list>
    /// </remarks>
    [HttpPost("{id:guid}/items/quick-add")]
    [Authorize]
    [ProducesResponseType<ReceiptDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> QuickAddItem(Guid id,
        [FromBody] QuickAddReceiptItemRequest request, CancellationToken ct = default)
    {
        var (receipt, error) = await LoadReceiptWithProcessAccessAsync(id, ct);
        if (error is not null) return error;

        if (receipt!.Status != ReceiptStatus.Processing)
            return UnprocessableEntity("root", ErrorCode.ReceiptInvalidStatusTransition,
                "Items can only be quick-added during Processing status.");

        var catalogItem = await db.CatalogItems.FindAsync([request.CatalogItemId], ct);
        if (catalogItem is null)
            return UnprocessableEntity("catalogItemId", ErrorCode.CatalogItemNotFound,
                "Catalog item not found.");

        if (catalogItem.IsArchived)
            return UnprocessableEntity("catalogItemId", ErrorCode.CatalogItemIsImmutable,
                "Archived catalog items cannot be added to a receipt.");

        if (catalogItem.Type is CatalogItemType.ProductGroup
                             or CatalogItemType.Variation
                             or CatalogItemType.Bundle)
            return UnprocessableEntity("catalogItemId", ErrorCode.ValidationError,
                $"Catalog item type '{catalogItem.Type}' cannot be added to a receipt.");

        if (receipt.Items.Any(i => i.CatalogItemId == request.CatalogItemId))
            return UnprocessableEntity("catalogItemId", ErrorCode.ValidationError,
                "This catalog item is already in the receipt.");

        var before = mapper.Map<ReceiptDto>(receipt);

        db.ReceiptItems.Add(new ReceiptItem
        {
            Id            = Guid.NewGuid(),
            ReceiptId     = receipt.Id,
            CatalogItemId = request.CatalogItemId,
            PlannedCount  = 0,
        });
        await db.SaveChangesAsync(ct);

        var nodeById = await LoadWarehouseNodesAsync(receipt.WarehouseId, ct);
        var updatedReceipt = await BaseQuery(includeItems: true).FirstAsync(r => r.Id == id, ct);
        var after = mapper.Map<ReceiptDto>(updatedReceipt, opts => opts.Items["nodeById"] = nodeById);
        await changeLog.CompareAndSaveToChangelog(before, after, ReceiptActions.ItemQuickAdded);

        return Ok(after);
    }

    // ── PUT items sync ────────────────────────────────────────────────────────

    /// <summary>Replace the full list of expected items. Allowed in Draft and Planned statuses.</summary>
    /// <remarks>
    /// Errors: 404 <c>receiptNotFound</c>; 422 <c>receiptInvalidStatusTransition</c> outside Draft or Planned;
    /// 422 <c>validationError</c> for a <c>catalogItemId</c> repeated in the request; 422
    /// <c>catalogItemNotFound</c> for an unknown catalog item; 403 <c>permissionDenied</c> /
    /// <c>receiptNotAssignedToWarehouse</c> (edit access).
    /// </remarks>
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
    /// <remarks>
    /// Requires <c>receipts.edit</c> or <c>receipts.process_assigned</c>. Errors: 404
    /// <c>receiptNotFound</c>; 404 <c>receiptItemNotFound</c> when the item does not belong to this receipt;
    /// 422 <c>receiptInvalidStatusTransition</c> outside Processing; 403 <c>permissionDenied</c> /
    /// <c>receiptNotAssignedToWarehouse</c>; 401 <c>tokenInvalid</c>.
    /// </remarks>
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
            .Include(i => i.CatalogItem).ThenInclude(c => c.Group)
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
    /// <remarks>
    /// Requires <c>receipts.edit</c> or <c>receipts.process_assigned</c>. The stock increase and the
    /// placement row are written in one transaction. Errors: 404 <c>receiptNotFound</c>; 404
    /// <c>receiptItemNotFound</c>; 422 <c>receiptInvalidStatusTransition</c> outside Processing; 422
    /// <c>storagePlaceNodeNotFound</c> for an unknown <c>storagePlaceNodeId</c>; 403
    /// <c>permissionDenied</c> / <c>receiptNotAssignedToWarehouse</c>; 401 <c>tokenInvalid</c>.
    /// </remarks>
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

        var action = receipt.Reason switch
        {
            ReceiptReason.NewGoods => InventoryActions.NewGoods,
            ReceiptReason.Return => InventoryActions.ReturnStock,
            _ => InventoryActions.UnknownAction
        };

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
                action: action,
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

    // ── POST placement / standard / batch ────────────────────────────────────

    /// <summary>Place multiple Standard items at the same storage node in one transaction. Only in Processing status.</summary>
    /// <remarks>
    /// Requires <c>receipts.edit</c> or <c>receipts.process_assigned</c>. Errors:
    /// <list type="bullet">
    ///   <item>404 <c>receiptNotFound</c>; 404 <c>receiptItemNotFound</c> for an id not in this receipt</item>
    ///   <item>422 <c>receiptInvalidStatusTransition</c> — receipt is not in Processing</item>
    ///   <item>422 <c>storagePlaceNodeNotFound</c> — unknown node, either from the up-front check or from the
    ///     inventory service inside the transaction</item>
    ///   <item>422 <c>validationError</c> — an <c>itemId</c> repeated in the request, or an item whose catalog
    ///     type is not Standard</item>
    ///   <item>403 <c>permissionDenied</c> / <c>receiptNotAssignedToWarehouse</c>; 401 <c>tokenInvalid</c></item>
    /// </list>
    /// </remarks>
    [HttpPost("{id:guid}/placements/standard/batch")]
    [Authorize]
    [ProducesResponseType<ReceiptDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> AddStandardPlacementBatch(Guid id,
        [FromBody] BatchStandardPlacementRequest request, CancellationToken ct = default)
    {
        var (receipt, error) = await LoadReceiptWithProcessAccessAsync(id, ct);
        if (error is not null) return error;

        if (receipt!.Status != ReceiptStatus.Processing)
            return UnprocessableEntity("root", ErrorCode.ReceiptInvalidStatusTransition,
                "Placements can only be added during Processing status.");

        var nodeExists = await db.StoragePlacesNodes.AnyAsync(n => n.Id == request.StoragePlaceNodeId, ct);
        if (!nodeExists)
            return UnprocessableEntity("storagePlaceNodeId", ErrorCode.StoragePlaceNodeNotFound,
                "Storage place node not found.");

        var duplicateIds = request.Items
            .GroupBy(x => x.ItemId)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();
        if (duplicateIds.Count > 0)
            return UnprocessableEntity("items", ErrorCode.ValidationError,
                $"Duplicate item id(s) in request: {string.Join(", ", duplicateIds)}.");

        var requestItemIds = request.Items.Select(i => i.ItemId).ToHashSet();
        var receiptItemsById = receipt.Items
            .Where(i => requestItemIds.Contains(i.Id))
            .ToDictionary(i => i.Id);
        
        var action = receipt.Reason switch
        {
            ReceiptReason.NewGoods => InventoryActions.NewGoods,
            ReceiptReason.Return => InventoryActions.ReturnStock,
            _ => InventoryActions.UnknownAction
        };

        foreach (var req in request.Items)
        {
            if (!receiptItemsById.TryGetValue(req.ItemId, out var item))
                return NotFound(ErrorCode.ReceiptItemNotFound, $"Receipt item '{req.ItemId}' not found.");

            if (item.CatalogItem.Type != CatalogItemType.Standard)
                return UnprocessableEntity("items", ErrorCode.ValidationError,
                    $"Item '{item.CatalogItem.Name}' is not Standard type and cannot be batch-placed.");
        }

        var warehouseId = receipt.WarehouseId;
        var nodeById = await LoadWarehouseNodesAsync(warehouseId, ct);

        var before = mapper.Map<ReceiptDto>(receipt, opts => opts.Items["nodeById"] = nodeById);

        var strategy = db.Database.CreateExecutionStrategy();
        try
        {
            await strategy.ExecuteAsync(async () =>
            {
                await using var tx = await db.Database.BeginTransactionAsync(ct);

                foreach (var req in request.Items)
                {
                    var item = receiptItemsById[req.ItemId];

                    await inventory.AddStandardItemsToNodeAsync(
                        request.StoragePlaceNodeId,
                        item.CatalogItemId,
                        req.Count,
                        action: action,
                        ct: ct);

                    db.ReceiptItemPlacements.Add(new ReceiptItemPlacement
                    {
                        Id                 = Guid.NewGuid(),
                        ReceiptItemId      = req.ItemId,
                        StoragePlaceNodeId = request.StoragePlaceNodeId,
                        Count              = req.Count,
                    });
                }

                await db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
            });
        }
        catch (StoragePlaceNodeNotFoundException)
        {
            return UnprocessableEntity("storagePlaceNodeId", ErrorCode.StoragePlaceNodeNotFound,
                "Storage place node not found.");
        }

        var updatedReceipt = await BaseQuery(includeItems: true).FirstAsync(r => r.Id == id, ct);
        var after = mapper.Map<ReceiptDto>(updatedReceipt, opts => opts.Items["nodeById"] = nodeById);
        await changeLog.CompareAndSaveToChangelog(before, after, ReceiptActions.BatchPlacementsAdded);

        return Ok(after);
    }

    // ── POST placement / unit ─────────────────────────────────────────────────

    /// <summary>Place a Unit (serialised) item at a storage node. Only in Processing status.</summary>
    /// <remarks>
    /// Requires <c>receipts.edit</c> or <c>receipts.process_assigned</c>. Errors: 404
    /// <c>receiptNotFound</c>; 404 <c>receiptItemNotFound</c>; 422
    /// <c>receiptInvalidStatusTransition</c> outside Processing; 422 <c>storagePlaceNodeNotFound</c>; 422
    /// <c>unitInventoryItemNumberDuplicate</c> on field <c>inventoryNumber</c> when the number is already used
    /// for this catalog item — raised by the soft check, and again by the unique index when two requests race;
    /// 403 <c>permissionDenied</c> / <c>receiptNotAssignedToWarehouse</c>; 401 <c>tokenInvalid</c>.
    /// </remarks>
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

        var action = receipt!.Reason switch
        {
            ReceiptReason.NewGoods => InventoryActions.NewGoods,
            ReceiptReason.Return => InventoryActions.ReturnStock,
            _ => InventoryActions.UnknownAction
        };
        
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
                    action: action,
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

    // ── DELETE placement ──────────────────────────────────────────────────────

    /// <summary>Remove a placement, reversing the inventory change. Only in Processing status.</summary>
    /// <remarks>
    /// Requires <c>receipts.edit</c> or <c>receipts.process_assigned</c>. Errors:
    /// <list type="bullet">
    ///   <item>404 <c>receiptNotFound</c>; 404 <c>receiptItemPlacementNotFound</c> when the placement does not
    ///     belong to this item</item>
    ///   <item>422 <c>receiptInvalidStatusTransition</c> — receipt is not in Processing</item>
    ///   <item>422 <c>inventoryItemMovedToAnotherNodeAfterPlacementCreated</c> — the unit item was moved out of
    ///     the placement's node since it was created</item>
    ///   <item>422 <c>unitInventoryItemNotFound</c> — the unit item is already gone</item>
    ///   <item>422 <c>insufficientInventory</c> — the standard stock to reverse is no longer in the node;
    ///     <c>args: { itemName, requested, available, missing, path }</c></item>
    ///   <item>403 <c>permissionDenied</c> / <c>receiptNotAssignedToWarehouse</c>; 401 <c>tokenInvalid</c></item>
    /// </list>
    /// </remarks>
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
        try
        {
            await strategy.ExecuteAsync(async () =>
            {
                await using var tx = await db.Database.BeginTransactionAsync(ct);

                if (placement.UnitInventoryItemId is not null)
                    await inventory.RemoveUnitItemAsync(
                        placement.UnitInventoryItemId.Value,
                        placement.StoragePlaceNodeId,
                        action: InventoryActions.CancelledPlacement,
                        ct: ct);
                else if (placement.Count > 0)
                    await inventory.RemoveStandardItemsFromNodeAsync(
                        placement.StoragePlaceNodeId,
                        itemEntity.CatalogItemId,
                        placement.Count,
                        action: InventoryActions.CancelledPlacement,
                        ct: ct);

                db.ReceiptItemPlacements.Remove(placement);
                await db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
            });
        }
        catch (InventoryItemNodeMismatchException)
        {
            return UnprocessableEntity("root", ErrorCode.InventoryItemMovedToAnotherNodeAfterPlacementCreated,
                "Товар был перемещён в другую ячейку после создания размещения. Обновите страницу.");
        }
        catch (UnitInventoryItemNotFoundException)
        {
            return UnprocessableEntity("root", ErrorCode.UnitInventoryItemNotFound,
                "Единичный товар не найден — возможно, он уже был удалён.");
        }
        catch (InsufficientInventoryException ex)
        {
            return UnprocessableEntity("root", ErrorCode.InsufficientInventory,
                $"Недостаточно товара для отмены размещения: доступно {ex.Available}, требуется {ex.Requested}.",
                ex.ToArgs());
        }

        var itemAfter = await LoadItemDtoAsync(itemId, receipt.WarehouseId, ct, nodeById);
        await changeLog.CompareAndSaveToChangelog(
            BuildItemChangelogSnapshot(receipt, itemBefore),
            BuildItemChangelogSnapshot(receipt, itemAfter),
            ReceiptActions.PlacementRemoved);

        return Ok(itemAfter);
    }

    // ── Status transitions ────────────────────────────────────────────────────

    /// <summary>Transition: Draft → Planned.</summary>
    /// <remarks>
    /// Draft status only. Errors: 404 <c>receiptNotFound</c>; 422 <c>receiptInvalidStatusTransition</c> from
    /// any other status; 403 <c>permissionDenied</c> / <c>receiptNotAssignedToWarehouse</c> (edit access).
    /// </remarks>
    [HttpPost("{id:guid}/plan")]
    [Authorize]
    [ProducesResponseType<ReceiptDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public Task<IActionResult> Plan(Guid id, CancellationToken ct = default) =>
        TransitionAsync(id, ReceiptStatus.Draft, ReceiptStatus.Planned, ReceiptActions.Planned, ct);

    /// <summary>Transition: Planned → Processing.</summary>
    /// <remarks>
    /// Planned status only. Errors: 404 <c>receiptNotFound</c>; 422 <c>receiptInvalidStatusTransition</c> from
    /// any other status; 403 <c>permissionDenied</c> / <c>receiptNotAssignedToWarehouse</c> (edit access).
    /// </remarks>
    [HttpPost("{id:guid}/start-processing")]
    [Authorize]
    [ProducesResponseType<ReceiptDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public Task<IActionResult> StartProcessing(Guid id, CancellationToken ct = default) =>
        TransitionAsync(id, ReceiptStatus.Planned, ReceiptStatus.Processing, ReceiptActions.ProcessingStarted, ct);

    /// <summary>Transition: Processing → Finished. Validates that each item with a received count has enough placements.</summary>
    /// <remarks>
    /// Processing status only; items without a <c>receivedCount</c> are not checked. Errors: 404
    /// <c>receiptNotFound</c>; 422 <c>receiptInvalidStatusTransition</c> from any other status; 422
    /// <c>receiptItemsUnderplaced</c> when an item has fewer placed units than received; 422
    /// <c>receiptItemsOverplaced</c> when it has more; 403 <c>permissionDenied</c> /
    /// <c>receiptNotAssignedToWarehouse</c> (edit access).
    /// </remarks>
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
    /// <remarks>
    /// Finished reverts to Processing. Errors: 404 <c>receiptNotFound</c>; 422 <c>receiptHasPlacements</c>
    /// when reverting from Processing while items still have placements; 422
    /// <c>receiptInvalidStatusTransition</c> from Draft or Canceled; 403 <c>permissionDenied</c> /
    /// <c>receiptNotAssignedToWarehouse</c> (edit access).
    /// </remarks>
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
    /// <remarks>
    /// Errors: 404 <c>receiptNotFound</c>; 422 <c>receiptInvalidStatusTransition</c> from Finished or
    /// Canceled; 422 <c>receiptHasPlacements</c> when cancelling from Processing while items still have
    /// placements; 403 <c>permissionDenied</c> / <c>receiptNotAssignedToWarehouse</c> (edit access).
    /// </remarks>
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
        if (AccessError(await Rule.PrecheckAsync(User, AccessLevel.Edit, ct)) is { } prelude)
            return (null, prelude);

        var receipt = await BaseQuery(includeItems).FirstOrDefaultAsync(r => r.Id == id, ct);
        if (receipt is null)
            return (null, NotFound(ErrorCode.ReceiptNotFound, "Receipt not found."));

        if (AccessError(await Rule.CheckAsync(User, AccessLevel.Edit, receipt, ct)) is { } denied)
            return (null, denied);

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
            .Include(i => i.CatalogItem).ThenInclude(c => c.Group)
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
