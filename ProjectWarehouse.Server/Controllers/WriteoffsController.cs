using System.ComponentModel.DataAnnotations;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProjectWarehouse.Server.Data;
using ProjectWarehouse.Server.Domain;
using ProjectWarehouse.Server.Infrastructure;
using ProjectWarehouse.Server.Infrastructure.Access;
using ProjectWarehouse.Server.Infrastructure.ChangeLog;
using ProjectWarehouse.Server.Infrastructure.Observability;
using ProjectWarehouse.Server.Models;
using ProjectWarehouse.Server.Models.Writeoffs;
using ProjectWarehouse.Server.Services;

namespace ProjectWarehouse.Server.Controllers;

[Route("api/writeoffs")]
public class WriteoffsController(
    ApplicationDbContext db,
    IMapper mapper,
    IInventoryService inventory,
    EntityAccessRegistry access,
    IChangeLogService<WriteoffDto> changeLog) : AppControllerBase
{
    private EntityAccessRule<Writeoff> Rule => access.For<Writeoff>();

    // ── Helpers ───────────────────────────────────────────────────────────────

    private IQueryable<Writeoff> BaseQuery(bool includeItems = false)
    {
        var q = db.Writeoffs
            .Include(w => w.Warehouse)
            .AsQueryable();

        if (includeItems)
            q = q
                .Include(w => w.Items)
                .ThenInclude(i => i.SourceNode)
                .ThenInclude(n => n.RootStoragePlace)
                .Include(w => w.Items)
                .ThenInclude(i => i.CatalogItem)
                .Include(w => w.Items)
                .ThenInclude(i => i.UnitInventoryItem)
                .ThenInclude(u => u!.CatalogItem);

        return q;
    }

    private async Task<(Writeoff? writeoff, IActionResult? error)> LoadWriteoffWithAccessAsync(
        Guid id, AccessLevel level, CancellationToken ct, bool includeItems = false)
    {
        if (AccessError(await Rule.PrecheckAsync(User, level, ct)) is { } prelude)
            return (null, prelude);

        var writeoff = await BaseQuery(includeItems).FirstOrDefaultAsync(w => w.Id == id, ct);
        if (writeoff is null)
            return (null, NotFound(ErrorCode.WriteoffNotFound, "Write-off not found."));

        if (AccessError(await Rule.CheckAsync(User, level, writeoff, ct)) is { } denied)
            return (null, denied);

        return (writeoff, null);
    }

    private Task<(Writeoff? writeoff, IActionResult? error)> LoadWriteoffWithEditAccessAsync(
        Guid id, CancellationToken ct, bool includeItems = false) =>
        LoadWriteoffWithAccessAsync(id, AccessLevel.Edit, ct, includeItems);

    private async Task<Dictionary<Guid, StoragePlaceNode>> LoadWarehouseNodesAsync(
        Guid warehouseId, CancellationToken ct) =>
        await db.StoragePlacesNodes
            .Where(n => n.RootStoragePlace.WarehouseId == warehouseId)
            .Include(n => n.RootStoragePlace)
            .ToDictionaryAsync(n => n.Id, ct);

    private WriteoffDto MapWithNodes(Writeoff writeoff, Dictionary<Guid, StoragePlaceNode> nodeById) =>
        mapper.Map<WriteoffDto>(writeoff, opts => opts.Items["nodeById"] = nodeById);

    // ── GET list ──────────────────────────────────────────────────────────────

    /// <summary>List write-offs with pagination, filtering, and search.</summary>
    /// <remarks>
    /// Query params: <c>page</c> (default 1), <c>pageSize</c> (default 20, max 200), <c>searchString</c>,
    /// <c>warehouseId</c>, <c>status</c>, <c>reason</c>, <c>sortBy</c> (default <c>Number</c>),
    /// <c>sortOrder</c> (default <c>Desc</c>).
    /// Requires <c>writeoffs.view</c> or <c>writeoffs.view_assigned</c>; without either, 403
    /// <c>permissionDenied</c>. 401 <c>tokenInvalid</c> when an <c>_assigned</c> permission is used but the
    /// token carries no resolvable user.
    /// </remarks>
    [HttpGet]
    [Authorize]
    [ProducesResponseType<Paginated<WriteoffSummaryDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(
        [FromQuery][Range(1, int.MaxValue)] int page = 1,
        [FromQuery][Range(1, 200)] int pageSize = 20,
        [FromQuery] string? searchString = null,
        [FromQuery] Guid? warehouseId = null,
        [FromQuery] WriteoffStatus? status = null,
        [FromQuery] WriteoffReason? reason = null,
        [FromQuery] WriteoffSortBy sortBy = WriteoffSortBy.Number,
        [FromQuery] SortOrder sortOrder = SortOrder.Desc,
        CancellationToken ct = default)
    {
        if (AccessError(await Rule.PrecheckAsync(User, AccessLevel.View, ct)) is { } error)
            return error;

        var accessible = await Rule.QueryAsync(User, AccessLevel.View, ct);

        var baseQuery = accessible
            .Include(w => w.Warehouse)
            .Include(w => w.Items)
            .Where(w => warehouseId == null || w.WarehouseId == warehouseId)
            .Where(w => status == null || w.Status == status)
            .Where(w => reason == null || w.Reason == reason)
            .WhereMatchesSearch(w => w.SearchString, searchString);

        var query = sortBy switch
        {
            WriteoffSortBy.Status        => baseQuery.Sort(w => w.Status, sortOrder).ThenBy(w => w.Id),
            WriteoffSortBy.CreatedAt     => baseQuery.Sort(w => w.CreatedAt, sortOrder).ThenBy(w => w.Id),
            WriteoffSortBy.WarehouseName => baseQuery.Sort(w => w.Warehouse.Name, sortOrder).ThenBy(w => w.Id),
            WriteoffSortBy.Name          => baseQuery.Sort(w => w.Name, sortOrder).ThenBy(w => w.Id),
            _                            => baseQuery.Sort(w => w.Number, sortOrder).ThenBy(w => w.Id),
        };

        var paginated = await query
            .ProjectTo<WriteoffSummaryDto>(mapper.ConfigurationProvider)
            .ToPaginatedAsync(page, pageSize, ct);

        return Ok(paginated);
    }

    // ── GET single ────────────────────────────────────────────────────────────

    /// <summary>Get full write-off details including items.</summary>
    /// <remarks>
    /// Errors: 404 <c>writeoffNotFound</c>; 403 <c>permissionDenied</c> without a view permission, or
    /// 403 <c>writeoffNotAssignedToWarehouse</c> when only <c>writeoffs.view_assigned</c> is held and the
    /// document belongs to another warehouse; 401 <c>tokenInvalid</c> for an unresolvable user.
    /// </remarks>
    [HttpGet("{id:guid}")]
    [Authorize]
    [ProducesResponseType<WriteoffDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct = default)
    {
        var (writeoff, error) = await LoadWriteoffWithAccessAsync(id, AccessLevel.View, ct, includeItems: true);
        if (error is not null) return error;

        var nodeById = await LoadWarehouseNodesAsync(writeoff!.WarehouseId, ct);
        return Ok(MapWithNodes(writeoff, nodeById));
    }

    // ── POST create ───────────────────────────────────────────────────────────

    /// <summary>Create a new write-off in Draft status.</summary>
    /// <remarks>
    /// Body: <c>CreateWriteoffRequest</c> — warehouseId (required), name, reason, notes.
    /// Errors: 422 <c>warehouseNotFound</c> for an unknown warehouse; 403 <c>permissionDenied</c> without
    /// <c>writeoffs.edit</c>/<c>writeoffs.edit_assigned</c>, or 403 <c>writeoffNotAssignedToWarehouse</c>
    /// when only <c>writeoffs.edit_assigned</c> is held and the target warehouse is not assigned.
    /// </remarks>
    [HttpPost]
    [Authorize]
    [ProducesResponseType<WriteoffDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Create([FromBody] CreateWriteoffRequest request, CancellationToken ct = default)
    {
        if (AccessError(await Rule.PrecheckAsync(User, AccessLevel.Edit, ct)) is { } error)
            return error;

        var warehouse = await db.Warehouses.FindAsync([request.WarehouseId], ct);
        if (warehouse is null)
            return UnprocessableEntity("warehouseId", ErrorCode.WarehouseNotFound, "Warehouse not found.");

        if (AccessError(await Rule.CheckWarehouseAsync(User, AccessLevel.Edit, request.WarehouseId, ct)) is { } denied)
            return denied;

        var writeoff = new Writeoff
        {
            Id          = Guid.NewGuid(),
            Name        = request.Name,
            Reason      = request.Reason,
            Notes       = request.Notes,
            WarehouseId = request.WarehouseId,
            CreatedById = GetCurrentUserId(),
            CreatedAt   = DateTime.UtcNow,
            Status      = WriteoffStatus.Draft,
        };

        db.Writeoffs.Add(writeoff);
        await db.SaveChangesAsync(ct);

        await db.Entry(writeoff).Reference(w => w.Warehouse).LoadAsync(ct);

        var dto = mapper.Map<WriteoffDto>(writeoff);
        await changeLog.CompareAndSaveToChangelog(null, dto);

        return CreatedAtAction(nameof(GetById), new { id = writeoff.Id }, dto);
    }

    // ── PATCH update ──────────────────────────────────────────────────────────

    /// <summary>Update write-off name, reason, notes. Only allowed in Draft status.</summary>
    /// <remarks>
    /// Errors: 404 <c>writeoffNotFound</c>; 422 <c>writeoffNotDraft</c> outside Draft status; 403
    /// <c>permissionDenied</c> or <c>writeoffNotAssignedToWarehouse</c> (edit access).
    /// </remarks>
    [HttpPatch("{id:guid}")]
    [Authorize]
    [ProducesResponseType<WriteoffDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateWriteoffRequest request,
        CancellationToken ct = default)
    {
        var (writeoff, error) = await LoadWriteoffWithEditAccessAsync(id, ct);
        if (error is not null) return error;

        if (writeoff!.Status != WriteoffStatus.Draft)
            return UnprocessableEntity("root", ErrorCode.WriteoffNotDraft,
                "Write-off can only be updated in Draft status.");

        var before = mapper.Map<WriteoffDto>(writeoff);

        writeoff.Name   = request.Name;
        writeoff.Reason = request.Reason;
        writeoff.Notes  = request.Notes;

        await db.SaveChangesAsync(ct);

        var after = mapper.Map<WriteoffDto>(writeoff);
        await changeLog.CompareAndSaveToChangelog(before, after);

        return Ok(after);
    }

    // ── DELETE ────────────────────────────────────────────────────────────────

    /// <summary>Delete a write-off. Only allowed in Draft status.</summary>
    /// <remarks>
    /// Errors: 404 <c>writeoffNotFound</c>; 422 <c>writeoffNotDraft</c> outside Draft status; 403
    /// <c>permissionDenied</c> or <c>writeoffNotAssignedToWarehouse</c> (edit access).
    /// </remarks>
    [HttpDelete("{id:guid}")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct = default)
    {
        var (writeoff, error) = await LoadWriteoffWithEditAccessAsync(id, ct);
        if (error is not null) return error;

        if (writeoff!.Status != WriteoffStatus.Draft)
            return UnprocessableEntity("root", ErrorCode.WriteoffNotDraft,
                "Only Draft write-offs can be deleted.");

        var dto = mapper.Map<WriteoffDto>(writeoff);

        db.Writeoffs.Remove(writeoff);
        await db.SaveChangesAsync(ct);

        await changeLog.CompareAndSaveToChangelog(dto, null);

        return NoContent();
    }

    // ── PUT items sync ────────────────────────────────────────────────────────

    /// <summary>Replace the full list of items to write off. Only allowed in Draft status.</summary>
    /// <remarks>
    /// Each line is either standard (<c>catalogItemId</c> + <c>count</c>) or unit
    /// (<c>unitInventoryItemId</c>), never both, and carries a <c>sourceNodeId</c> in this write-off's warehouse.
    /// Errors:
    /// <list type="bullet">
    ///   <item>404 <c>writeoffNotFound</c></item>
    ///   <item>422 <c>writeoffNotDraft</c> — outside Draft status</item>
    ///   <item>422 <c>validationError</c> — neither or both discriminators set, or two standard lines with the
    ///     same catalog item and source node</item>
    ///   <item>422 <c>outOfRange</c> — <c>count</c> not greater than zero</item>
    ///   <item>422 <c>storagePlaceNodeNotFound</c> — <c>sourceNodeId</c> is not a node of this warehouse</item>
    ///   <item>422 <c>unitInventoryItemNotFound</c> — the unit item does not sit at the given source node</item>
    ///   <item>422 <c>catalogItemNotFound</c> — unknown catalog item</item>
    ///   <item>403 <c>permissionDenied</c> / <c>writeoffNotAssignedToWarehouse</c> (edit access)</item>
    /// </list>
    /// </remarks>
    [HttpPut("{id:guid}/items")]
    [Authorize]
    [ProducesResponseType<WriteoffDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> SyncItems(Guid id, [FromBody] IReadOnlyList<WriteoffItemRequest> items,
        CancellationToken ct = default)
    {
        var (writeoff, error) = await LoadWriteoffWithEditAccessAsync(id, ct, includeItems: true);
        if (error is not null) return error;

        if (writeoff!.Status != WriteoffStatus.Draft)
            return UnprocessableEntity("root", ErrorCode.WriteoffNotDraft,
                "Items can only be modified in Draft status.");

        // Validate each item's discriminator and source node
        for (var i = 0; i < items.Count; i++)
        {
            var req = items[i];
            var prefix = $"items[{i}]";

            var isStandard = req.CatalogItemId.HasValue && req.Count.HasValue;
            var isUnit     = req.UnitInventoryItemId.HasValue;

            var setCount = (isStandard ? 1 : 0) + (isUnit ? 1 : 0);
            if (setCount != 1)
                return UnprocessableEntity(prefix, ErrorCode.ValidationError,
                    "Exactly one of (catalogItemId+count) or unitInventoryItemId must be provided.");

            if (isStandard && req.Count!.Value <= 0)
                return UnprocessableEntity($"{prefix}.count", ErrorCode.OutOfRange,
                    "Count must be greater than zero.");

            var nodeExists = await db.StoragePlacesNodes.AnyAsync(
                n => n.Id == req.SourceNodeId && n.RootStoragePlace.WarehouseId == writeoff.WarehouseId, ct);
            if (!nodeExists)
                return UnprocessableEntity($"{prefix}.sourceNodeId", ErrorCode.StoragePlaceNodeNotFound,
                    $"Storage node '{req.SourceNodeId}' not found in this warehouse.");
        }

        // Reject duplicate standard items (same catalogItemId + sourceNodeId)
        var standardDuplicates = items
            .Where(x => x.CatalogItemId.HasValue)
            .GroupBy(x => (x.CatalogItemId, x.SourceNodeId))
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();
        if (standardDuplicates.Count > 0)
            return UnprocessableEntity("root", ErrorCode.ValidationError,
                $"Duplicate standard item(s) with the same source node in request.");

        // Verify unit/bundle items exist at their source nodes
        foreach (var req in items.Where(r => r.UnitInventoryItemId.HasValue))
        {
            var exists = await db.InventoryItems.OfType<UnitInventoryItem>()
                .AnyAsync(u => u.Id == req.UnitInventoryItemId && u.StoragePlaceNodeId == req.SourceNodeId, ct);
            if (!exists)
                return UnprocessableEntity("root", ErrorCode.UnitInventoryItemNotFound,
                    $"Unit inventory item '{req.UnitInventoryItemId}' not found at node '{req.SourceNodeId}'.");
        }

        var before = await BuildDtoAsync(writeoff, ct);

        // Remove all existing items and replace with the new list (sync pattern)
        db.WriteoffItems.RemoveRange(writeoff.Items);
        writeoff.Items.Clear();

        foreach (var req in items)
        {
            var item = new WriteoffItem
            {
                Id           = Guid.NewGuid(),
                WriteoffId   = writeoff.Id,
                SourceNodeId = req.SourceNodeId,
                Notes        = req.Notes,
            };

            if (req.CatalogItemId.HasValue)
            {
                var catalogItem = await db.CatalogItems.FindAsync([req.CatalogItemId.Value], ct);
                if (catalogItem is null)
                    return UnprocessableEntity("root", ErrorCode.CatalogItemNotFound,
                        $"Catalog item '{req.CatalogItemId}' not found.");
                item.CatalogItemId = req.CatalogItemId.Value;
                item.Count         = req.Count!.Value;
            }
            else
            {
                item.UnitInventoryItemId = req.UnitInventoryItemId!.Value;
            }

            db.WriteoffItems.Add(item);
        }

        await db.SaveChangesAsync(ct);

        // Reload from DB so new items have all navigation properties populated
        var reloaded = await BaseQuery(includeItems: true).FirstAsync(w => w.Id == id, ct);
        var after = await BuildDtoAsync(reloaded, ct);
        await changeLog.CompareAndSaveToChangelog(before, after, WriteoffActions.ItemsSynced);

        return Ok(after);
    }

    // ── POST finish ───────────────────────────────────────────────────────────

    /// <summary>Finish the write-off: execute inventory removal for all items. Draft → Finished.</summary>
    /// <remarks>
    /// Draft status only; all removals run in one transaction, so a failure leaves stock untouched. Errors:
    /// <list type="bullet">
    ///   <item>404 <c>writeoffNotFound</c></item>
    ///   <item>422 <c>writeoffNotDraft</c> — not in Draft status</item>
    ///   <item>422 <c>writeoffHasNoItems</c> — nothing to write off</item>
    ///   <item>422 <c>writeoffInsufficientInventory</c> — a standard line asks for more than the source node
    ///     holds; <c>args: { itemName, requested, available, missing, path }</c>, <c>path</c> being the node
    ///     breadcrumb joined with <c>" / "</c></item>
    ///   <item>422 <c>inventoryItemNodeMismatch</c> — a unit item was moved out of its source node after the
    ///     document was built</item>
    ///   <item>422 <c>unitInventoryItemNotFound</c> — a unit item no longer exists</item>
    ///   <item>403 <c>permissionDenied</c> / <c>writeoffNotAssignedToWarehouse</c> (edit access)</item>
    /// </list>
    /// </remarks>
    [HttpPost("{id:guid}/finish")]
    [Authorize]
    [ProducesResponseType<WriteoffDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Finish(Guid id, CancellationToken ct = default)
    {
        var (writeoff, error) = await LoadWriteoffWithEditAccessAsync(id, ct, includeItems: true);
        if (error is not null) return error;

        if (writeoff!.Status != WriteoffStatus.Draft)
            return UnprocessableEntity("root", ErrorCode.WriteoffNotDraft,
                "Write-off must be in Draft status to finish.");

        if (writeoff.Items.Count == 0)
            return UnprocessableEntity("root", ErrorCode.WriteoffHasNoItems,
                "Write-off has no items.");

        var before = await BuildDtoAsync(writeoff, ct);

        try
        {
            await db.Database.ExecuteInTransactionAsync("writeoffs.finish", async () =>
            {
                // Reload inside the transaction so the status check sees the committed state
                var fresh = await BaseQuery(includeItems: true).FirstAsync(w => w.Id == id, ct);

                if (fresh.Status != WriteoffStatus.Draft)
                    return; // finished concurrently

                foreach (var item in fresh.Items)
                {
                    if (item.CatalogItemId.HasValue)
                    {
                        await inventory.RemoveStandardItemsFromNodeAsync(
                            item.SourceNodeId,
                            item.CatalogItemId.Value,
                            item.Count,
                            action: InventoryActions.WrittenOff,
                            ct: ct);
                    }
                    else if (item.UnitInventoryItemId.HasValue)
                    {
                        await inventory.RemoveUnitItemAsync(
                            item.UnitInventoryItemId.Value,
                            item.SourceNodeId,
                            action: InventoryActions.WrittenOff,
                            ct: ct);
                    }
                }

                fresh.Status = WriteoffStatus.Finished;
                await db.SaveChangesAsync(ct);
            }, ct);
        }
        catch (InsufficientInventoryException ex)
        {
            return UnprocessableEntity("root", ErrorCode.WriteoffInsufficientInventory,
                $"Insufficient inventory at node '{ex.NodeId}': requested {ex.Requested}, available {ex.Available}.",
                ex.ToArgs());
        }
        catch (InventoryItemNodeMismatchException)
        {
            return UnprocessableEntity("root", ErrorCode.InventoryItemNodeMismatch,
                "One or more items are no longer at the expected storage node.");
        }
        catch (UnitInventoryItemNotFoundException)
        {
            return UnprocessableEntity("root", ErrorCode.UnitInventoryItemNotFound,
                "One or more unit items were not found.");
        }

        var nodeById = await LoadWarehouseNodesAsync(writeoff.WarehouseId, ct);

        // Reload items after finish (unit FKs may be SetNull after removal)
        var updated = await BaseQuery(includeItems: true).FirstAsync(w => w.Id == id, ct);
        var after = MapWithNodes(updated, nodeById);
        await changeLog.CompareAndSaveToChangelog(before, after, WriteoffActions.Finished);

        return Ok(after);
    }

    // ── POST cancel ───────────────────────────────────────────────────────────

    /// <summary>Cancel the write-off. Only allowed in Draft status.</summary>
    /// <remarks>
    /// Errors: 404 <c>writeoffNotFound</c>; 422 <c>writeoffNotDraft</c> — reused for a document already
    /// Finished or Canceled; 403 <c>permissionDenied</c> / <c>writeoffNotAssignedToWarehouse</c> (edit access).
    /// </remarks>
    [HttpPost("{id:guid}/cancel")]
    [Authorize]
    [ProducesResponseType<WriteoffDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken ct = default)
    {
        var (writeoff, error) = await LoadWriteoffWithEditAccessAsync(id, ct, includeItems: true);
        if (error is not null) return error;

        if (writeoff!.Status is WriteoffStatus.Finished or WriteoffStatus.Canceled)
            return UnprocessableEntity("root", ErrorCode.WriteoffNotDraft,
                $"Cannot cancel a write-off in '{writeoff.Status}' status.");

        var before = await BuildDtoAsync(writeoff, ct);
        writeoff.Status = WriteoffStatus.Canceled;
        await db.SaveChangesAsync(ct);

        var after = await BuildDtoAsync(writeoff, ct);
        await changeLog.CompareAndSaveToChangelog(before, after, WriteoffActions.Canceled);

        return Ok(after);
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private async Task<WriteoffDto> BuildDtoAsync(Writeoff writeoff, CancellationToken ct)
    {
        var nodeById = await LoadWarehouseNodesAsync(writeoff.WarehouseId, ct);
        return MapWithNodes(writeoff, nodeById);
    }
}
