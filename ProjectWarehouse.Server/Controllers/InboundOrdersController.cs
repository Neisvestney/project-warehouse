using System.ComponentModel.DataAnnotations;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProjectWarehouse.Server.Data;
using ProjectWarehouse.Server.Domain;
using ProjectWarehouse.Server.Infrastructure;
using ProjectWarehouse.Server.Infrastructure.ChangeLog;
using ProjectWarehouse.Server.Models;
using ProjectWarehouse.Server.Models.InboundOrders;
using ProjectWarehouse.Server.Models.Warehouses;

namespace ProjectWarehouse.Server.Controllers;

[Route("api/inbound-orders")]
public class InboundOrdersController(
    ApplicationDbContext db,
    IMapper mapper,
    IChangeLogService<InboundOrderDto> changeLog) : AppControllerBase
{
    /// <summary>List inbound orders (paginated, searchable, filterable).</summary>
    [HttpGet]
    [Authorize]
    [ProducesResponseType<Paginated<InboundOrderSummaryDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(
        [FromQuery][Range(1, int.MaxValue)] int page = 1,
        [FromQuery][Range(1, 200)] int pageSize = 20,
        [FromQuery] string? searchString = null,
        [FromQuery] Guid? warehouseId = null,
        CancellationToken ct = default)
    {
        var canViewAll = User.HasClaim("permission", Permissions.InboundOrders.View);
        var canViewAssigned = User.HasClaim("permission", Permissions.InboundOrders.ViewAssignedWarehouses);

        if (!canViewAll && !canViewAssigned)
            return Forbidden();

        var query = db.InboundOrders
            .WhereMatchesSearch(o => o.SearchString, searchString);

        if (warehouseId.HasValue)
            query = query.Where(o => o.WarehouseId == warehouseId.Value);

        if (!canViewAll)
        {
            var assignedIds = await GetCurrentUserAssignedWarehouseIdsAsync(db, ct);
            if (assignedIds is null)
                return Unauthorized(ErrorCode.TokenInvalid, "Invalid token.");
            query = query.Where(o => assignedIds.Contains(o.WarehouseId));
        }

        var paginated = await query
            .OrderByDescending(o => o.PlannedStartDateTime)
            .ProjectTo<InboundOrderSummaryDto>(mapper.ConfigurationProvider)
            .ToPaginatedAsync(page, pageSize, ct);

        return Ok(paginated);
    }

    /// <summary>Get an inbound order by ID (all fields except item groups).</summary>
    [HttpGet("{id:guid}")]
    [Authorize]
    [ProducesResponseType<InboundOrderDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct = default)
    {
        var canViewAll = User.HasClaim("permission", Permissions.InboundOrders.View);
        var canViewAssigned = User.HasClaim("permission", Permissions.InboundOrders.ViewAssignedWarehouses);

        if (!canViewAll && !canViewAssigned)
            return Forbidden();

        if (!canViewAll)
        {
            var assignedIds = await GetCurrentUserAssignedWarehouseIdsAsync(db, ct);
            if (assignedIds is null)
                return Unauthorized(ErrorCode.TokenInvalid, "Invalid token.");

            var warehouseId = await db.InboundOrders
                .Where(o => o.Id == id)
                .Select(o => (Guid?)o.WarehouseId)
                .FirstOrDefaultAsync(ct);
            if (warehouseId is null)
                return NotFound(ErrorCode.InboundOrderNotFound, "Inbound order not found.");
            if (!assignedIds.Contains(warehouseId.Value))
                return Forbidden();
        }

        var dto = await db.InboundOrders
            .ProjectTo<InboundOrderDto>(mapper.ConfigurationProvider)
            .FirstOrDefaultAsync(o => o.Id == id, ct);

        if (dto is null)
            return NotFound(ErrorCode.InboundOrderNotFound, "Inbound order not found.");

        return Ok(dto);
    }

    /// <summary>Create a new inbound order (always in Draft status).</summary>
    [HttpPost]
    [Authorize]
    [ProducesResponseType<InboundOrderDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Create([FromBody] CreateInboundOrderRequest request, CancellationToken ct = default)
    {
        var canEditAll = User.HasClaim("permission", Permissions.InboundOrders.Edit);
        var canEditAssigned = User.HasClaim("permission", Permissions.InboundOrders.EditAssignedWarehouses);

        if (!canEditAll && !canEditAssigned)
            return Forbidden();

        if (!canEditAll)
        {
            var assignedIds = await GetCurrentUserAssignedWarehouseIdsAsync(db, ct);
            if (assignedIds is null)
                return Unauthorized(ErrorCode.TokenInvalid, "Invalid token.");
            if (!assignedIds.Contains(request.WarehouseId))
                return Forbidden();
        }

        var warehouseExists = await db.Warehouses.AnyAsync(w => w.Id == request.WarehouseId, ct);
        if (!warehouseExists)
            return UnprocessableEntity("warehouseId", ErrorCode.WarehouseNotFound, "Warehouse not found.");

        var assignedUsers = await db.Users
            .Where(u => request.AssignedUserIds.Contains(u.Id))
            .ToListAsync(ct);

        if (assignedUsers.Count != request.AssignedUserIds.Distinct().Count())
            return UnprocessableEntity("assignedUserIds", ErrorCode.UserNotFound, "One or more users not found.");

        var order = new InboundOrder
        {
            Id = Guid.NewGuid(),
            WarehouseId = request.WarehouseId,
            Title = request.Title,
            PlannedStartDateTime = request.PlannedStartDateTime,
            Notes = request.Notes,
            Status = InboundOrderStatus.Draft,
            AssignedUsers = assignedUsers
        };

        db.InboundOrders.Add(order);
        await db.SaveChangesAsync(ct);

        var dto = await db.InboundOrders
            .ProjectTo<InboundOrderDto>(mapper.ConfigurationProvider)
            .FirstAsync(o => o.Id == order.Id, ct);

        await changeLog.CompareAndSaveToChangelog(null, dto);

        return CreatedAtAction(nameof(GetById), new { id = order.Id }, dto);
    }

    /// <summary>Update an inbound order.</summary>
    [HttpPut("{id:guid}")]
    [Authorize]
    [ProducesResponseType<InboundOrderDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateInboundOrderRequest request, CancellationToken ct = default)
    {
        var canEditAll = User.HasClaim("permission", Permissions.InboundOrders.Edit);
        var canEditAssigned = User.HasClaim("permission", Permissions.InboundOrders.EditAssignedWarehouses);

        if (!canEditAll && !canEditAssigned)
            return Forbidden();

        var order = await db.InboundOrders
            .Include(o => o.AssignedUsers)
            .FirstOrDefaultAsync(o => o.Id == id, ct);

        if (order is null)
            return NotFound(ErrorCode.InboundOrderNotFound, "Inbound order not found.");

        if (!canEditAll)
        {
            var assignedIds = await GetCurrentUserAssignedWarehouseIdsAsync(db, ct);
            if (assignedIds is null)
                return Unauthorized(ErrorCode.TokenInvalid, "Invalid token.");
            if (!assignedIds.Contains(order.WarehouseId) || !assignedIds.Contains(request.WarehouseId))
                return Forbidden();
        }

        var warehouseExists = await db.Warehouses.AnyAsync(w => w.Id == request.WarehouseId, ct);
        if (!warehouseExists)
            return UnprocessableEntity("warehouseId", ErrorCode.WarehouseNotFound, "Warehouse not found.");

        var assignedUsers = await db.Users
            .Where(u => request.AssignedUserIds.Contains(u.Id))
            .ToListAsync(ct);

        if (assignedUsers.Count != request.AssignedUserIds.Distinct().Count())
            return UnprocessableEntity("assignedUserIds", ErrorCode.UserNotFound, "One or more users not found.");

        var beforeDto = await db.InboundOrders
            .ProjectTo<InboundOrderDto>(mapper.ConfigurationProvider)
            .FirstAsync(o => o.Id == id, ct);

        order.WarehouseId = request.WarehouseId;
        order.Title = request.Title;
        order.PlannedStartDateTime = request.PlannedStartDateTime;
        order.Notes = request.Notes;
        order.AssignedUsers.Clear();
        foreach (var user in assignedUsers)
            order.AssignedUsers.Add(user);

        await db.SaveChangesAsync(ct);

        var afterDto = await db.InboundOrders
            .ProjectTo<InboundOrderDto>(mapper.ConfigurationProvider)
            .FirstAsync(o => o.Id == id, ct);

        await changeLog.CompareAndSaveToChangelog(beforeDto, afterDto);

        return Ok(afterDto);
    }

    /// <summary>Delete an inbound order (only Draft or Finished status).</summary>
    [HttpDelete("{id:guid}")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct = default)
    {
        var canEditAll = User.HasClaim("permission", Permissions.InboundOrders.Edit);
        var canEditAssigned = User.HasClaim("permission", Permissions.InboundOrders.EditAssignedWarehouses);

        if (!canEditAll && !canEditAssigned)
            return Forbidden();

        var order = await db.InboundOrders
            .Include(o => o.DeclaredItemsGroups)
            .Include(o => o.ProcessedItemsGroups)
            .FirstOrDefaultAsync(o => o.Id == id, ct);

        if (order is null)
            return NotFound(ErrorCode.InboundOrderNotFound, "Inbound order not found.");

        if (!canEditAll)
        {
            var assignedIds = await GetCurrentUserAssignedWarehouseIdsAsync(db, ct);
            if (assignedIds is null)
                return Unauthorized(ErrorCode.TokenInvalid, "Invalid token.");
            if (!assignedIds.Contains(order.WarehouseId))
                return Forbidden();
        }

        if (order.Status == InboundOrderStatus.Processing)
            return Conflict(ErrorCode.InboundOrderInvalidStatus, "Cannot delete an order with status Processing.");

        var beforeDto = await db.InboundOrders
            .ProjectTo<InboundOrderDto>(mapper.ConfigurationProvider)
            .FirstAsync(o => o.Id == id, ct);

        db.InboundOrderDeclaredItemsGroups.RemoveRange(order.DeclaredItemsGroups);
        db.InboundOrderProcessedItemsGroups.RemoveRange(order.ProcessedItemsGroups);
        db.InboundOrders.Remove(order);
        await db.SaveChangesAsync(ct);

        await changeLog.CompareAndSaveToChangelog(beforeDto, null);

        return NoContent();
    }

    /// <summary>Get all draft item groups for an inbound order.</summary>
    [HttpGet("{id:guid}/draft-items-groups")]
    [Authorize]
    [ProducesResponseType<IReadOnlyList<InboundOrderDraftItemsGroupDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDraftItemsGroups(Guid id, CancellationToken ct = default)
    {
        var canViewAll = User.HasClaim("permission", Permissions.InboundOrders.View);
        var canViewAssigned = User.HasClaim("permission", Permissions.InboundOrders.ViewAssignedWarehouses);

        if (!canViewAll && !canViewAssigned)
            return Forbidden();

        var order = await db.InboundOrders.FirstOrDefaultAsync(o => o.Id == id, ct);
        if (order is null)
            return NotFound(ErrorCode.InboundOrderNotFound, "Inbound order not found.");

        if (!canViewAll)
        {
            var assignedIds = await GetCurrentUserAssignedWarehouseIdsAsync(db, ct);
            if (assignedIds is null)
                return Unauthorized(ErrorCode.TokenInvalid, "Invalid token.");
            if (!assignedIds.Contains(order.WarehouseId))
                return Forbidden();
        }

        var groups = await db.InboundOrderDraftItemsGroups
            .Where(g => g.InboundOrderId == id)
            .OrderBy(g => g.Order)
            .ProjectTo<InboundOrderDraftItemsGroupDto>(mapper.ConfigurationProvider)
            .ToListAsync(ct);

        return Ok(groups);
    }

    /// <summary>Atomically sync draft item groups for an inbound order (Draft status only).</summary>
    [HttpPut("{id:guid}/draft-items-groups")]
    [Authorize]
    [ProducesResponseType<IReadOnlyList<InboundOrderDraftItemsGroupDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> UpdateDraftItemsGroups(
        Guid id,
        [FromBody] UpdateDraftItemsGroupsRequest request,
        CancellationToken ct = default)
    {
        var canEditAll = User.HasClaim("permission", Permissions.InboundOrders.Edit);
        var canEditAssigned = User.HasClaim("permission", Permissions.InboundOrders.EditAssignedWarehouses);

        if (!canEditAll && !canEditAssigned)
            return Forbidden();

        var order = await db.InboundOrders
            .Include(o => o.DraftItemsGroups)
            .FirstOrDefaultAsync(o => o.Id == id, ct);

        if (order is null)
            return NotFound(ErrorCode.InboundOrderNotFound, "Inbound order not found.");

        if (!canEditAll)
        {
            var assignedIds = await GetCurrentUserAssignedWarehouseIdsAsync(db, ct);
            if (assignedIds is null)
                return Unauthorized(ErrorCode.TokenInvalid, "Invalid token.");
            if (!assignedIds.Contains(order.WarehouseId))
                return Forbidden();
        }

        if (order.Status != InboundOrderStatus.Draft)
            return Conflict(ErrorCode.InboundOrderInvalidStatus, "Draft items can only be edited when the order is in Draft status.");

        var existingDraftIds = order.DraftItemsGroups.Select(g => g.Id).ToHashSet();
        var unknownIds = request.DraftItemsGroups
            .Select((item, i) => (item, i))
            .Where(x => x.item.Id.HasValue && !existingDraftIds.Contains(x.item.Id.Value))
            .ToList();

        if (unknownIds.Count > 0)
        {
            var errors = unknownIds.Select(x =>
                (Field: $"draftItemsGroups[{x.i}].id", Code: ErrorCode.InboundOrderDraftItemsGroupNotFound,
                    Message: $"Draft item group '{x.item.Id}' does not belong to this order.",
                    Args: (IReadOnlyDictionary<string, object>?)null));
            return Problem(AppProblems.UnprocessableEntities(errors));
        }

        var catalogItemIds = request.DraftItemsGroups
            .Where(x => x.CatalogItemId.HasValue)
            .Select(x => x.CatalogItemId!.Value)
            .Distinct()
            .ToList();

        if (catalogItemIds.Count > 0)
        {
            var validCatalogItemIds = await db.CatalogItems
                .Where(c => catalogItemIds.Contains(c.Id))
                .Select(c => c.Id)
                .ToListAsync(ct);
            var invalidCatalogItems = request.DraftItemsGroups
                .Select((item, i) => (item, i))
                .Where(x => x.item.CatalogItemId.HasValue &&
                            !validCatalogItemIds.Contains(x.item.CatalogItemId.Value))
                .ToList();

            if (invalidCatalogItems.Count > 0)
            {
                var errors = invalidCatalogItems.Select(x =>
                    (Field: $"draftItemsGroups[{x.i}].catalogItemId",
                        Code: ErrorCode.CatalogItemNotFound,
                        Message: $"Catalog item '{x.item.CatalogItemId}' not found.",
                        Args: (IReadOnlyDictionary<string, object>?)null));
                return Problem(AppProblems.UnprocessableEntities(errors));
            }
        }

        var catalogIds = request.DraftItemsGroups
            .Where(x => x.CatalogItemWithCharacteristicId.HasValue)
            .Select(x => x.CatalogItemWithCharacteristicId!.Value)
            .Distinct()
            .ToList();

        if (catalogIds.Count > 0)
        {
            var validCatalogIds = await db.CatalogItemsWithCharacteristics
                .Where(c => catalogIds.Contains(c.Id))
                .Select(c => c.Id)
                .ToListAsync(ct);
            var invalidCatalog = request.DraftItemsGroups
                .Select((item, i) => (item, i))
                .Where(x => x.item.CatalogItemWithCharacteristicId.HasValue &&
                            !validCatalogIds.Contains(x.item.CatalogItemWithCharacteristicId.Value))
                .ToList();

            if (invalidCatalog.Count > 0)
            {
                var errors = invalidCatalog.Select(x =>
                    (Field: $"draftItemsGroups[{x.i}].catalogItemWithCharacteristicId",
                        Code: ErrorCode.CatalogItemCharacteristicNotFound,
                        Message: $"Catalog item with characteristic '{x.item.CatalogItemWithCharacteristicId}' not found.",
                        Args: (IReadOnlyDictionary<string, object>?)null));
                return Problem(AppProblems.UnprocessableEntities(errors));
            }
        }

        var incomingIds = request.DraftItemsGroups
            .Where(x => x.Id.HasValue)
            .Select(x => x.Id!.Value)
            .ToHashSet();

        var toDelete = order.DraftItemsGroups
            .Where(g => !incomingIds.Contains(g.Id))
            .ToList();

        db.InboundOrderDraftItemsGroups.RemoveRange(toDelete);

        foreach (var (item, i) in request.DraftItemsGroups.Select((item, i) => (item, i)))
        {
            if (item.Id.HasValue)
            {
                var existing = order.DraftItemsGroups.First(g => g.Id == item.Id.Value);
                existing.Name = item.Name;
                existing.Article = item.Article;
                existing.Barcode = item.Barcode;
                existing.RootBarcode = item.RootBarcode;
                existing.Characteristic = item.Characteristic;
                existing.Count = item.Count;
                existing.Order = i;
                existing.CatalogItemId = item.CatalogItemId;
                existing.CatalogItemWithCharacteristicId = item.CatalogItemWithCharacteristicId;
                existing.CreateNew = item.CreateNew;
            }
            else
            {
                db.InboundOrderDraftItemsGroups.Add(new InboundOrderDraftItemsGroup
                {
                    Id = Guid.NewGuid(),
                    InboundOrderId = id,
                    Name = item.Name,
                    Article = item.Article,
                    Barcode = item.Barcode,
                    RootBarcode = item.RootBarcode,
                    Characteristic = item.Characteristic,
                    Count = item.Count,
                    Order = i,
                    CatalogItemId = item.CatalogItemId,
                    CatalogItemWithCharacteristicId = item.CatalogItemWithCharacteristicId,
                    CreateNew = item.CreateNew
                });
            }
        }

        await db.SaveChangesAsync(ct);

        var groups = await db.InboundOrderDraftItemsGroups
            .Where(g => g.InboundOrderId == id)
            .OrderBy(g => g.Order)
            .ProjectTo<InboundOrderDraftItemsGroupDto>(mapper.ConfigurationProvider)
            .ToListAsync(ct);

        return Ok(groups);
    }

    /// <summary>Get declared, processed, and diff summary for an inbound order.</summary>
    [HttpGet("{id:guid}/items-comparison")]
    [Authorize]
    [ProducesResponseType<InboundOrderItemsComparisonDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetItemsComparison(Guid id, CancellationToken ct = default)
    {
        var canViewAll = User.HasClaim("permission", Permissions.InboundOrders.View);
        var canViewAssigned = User.HasClaim("permission", Permissions.InboundOrders.ViewAssignedWarehouses);

        if (!canViewAll && !canViewAssigned)
            return Forbidden();

        var order = await db.InboundOrders.FirstOrDefaultAsync(o => o.Id == id, ct);
        if (order is null)
            return NotFound(ErrorCode.InboundOrderNotFound, "Inbound order not found.");

        if (!canViewAll)
        {
            var assignedIds = await GetCurrentUserAssignedWarehouseIdsAsync(db, ct);
            if (assignedIds is null)
                return Unauthorized(ErrorCode.TokenInvalid, "Invalid token.");
            if (!assignedIds.Contains(order.WarehouseId))
                return Forbidden();
        }

        var declared = await db.InboundOrderDeclaredItemsGroups
            .Where(g => g.InboundOrderId == id)
            .Select(g => new
            {
                g.CatalogItemWithCharacteristicId,
                g.Count,
                Characteristic = g.CatalogItemWithCharacteristic.Characteristic,
                Barcode = g.CatalogItemWithCharacteristic.Barcode,
                CatalogItemId = g.CatalogItemWithCharacteristic.CatalogItem.Id,
                CatalogItemName = g.CatalogItemWithCharacteristic.CatalogItem.Name,
                CatalogItemArticle = g.CatalogItemWithCharacteristic.CatalogItem.Article,
                CatalogItemBarcode = g.CatalogItemWithCharacteristic.CatalogItem.Barcode,
            })
            .ToListAsync(ct);

        var processed = await db.InboundOrderProcessedItemsGroups
            .Where(g => g.InboundOrderId == id)
            .GroupBy(g => new
            {
                g.CatalogItemWithCharacteristicId,
                Characteristic = g.CatalogItemWithCharacteristic.Characteristic,
                Barcode = g.CatalogItemWithCharacteristic.Barcode,
                CatalogItemId = g.CatalogItemWithCharacteristic.CatalogItem.Id,
                CatalogItemName = g.CatalogItemWithCharacteristic.CatalogItem.Name,
                CatalogItemArticle = g.CatalogItemWithCharacteristic.CatalogItem.Article,
                CatalogItemBarcode = g.CatalogItemWithCharacteristic.CatalogItem.Barcode,
            })
            .Select(g => new
            {
                g.Key.CatalogItemWithCharacteristicId,
                Count = g.Sum(x => x.Count),
                g.Key.Characteristic,
                g.Key.Barcode,
                g.Key.CatalogItemId,
                g.Key.CatalogItemName,
                g.Key.CatalogItemArticle,
                g.Key.CatalogItemBarcode,
            })
            .ToListAsync(ct);

        var allIds = declared.Select(d => d.CatalogItemWithCharacteristicId)
            .Union(processed.Select(p => p.CatalogItemWithCharacteristicId));

        var declaredByCharId = declared.ToDictionary(d => d.CatalogItemWithCharacteristicId);
        var processedByCharId = processed.ToDictionary(p => p.CatalogItemWithCharacteristicId);

        NodeCharacteristicDto BuildCharDto(Guid charId, string characteristic, string? barcode,
            Guid catalogItemId, string catalogItemName, string catalogItemArticle, string? catalogItemBarcode) =>
            new()
            {
                Id = charId,
                Characteristic = characteristic,
                Barcode = barcode,
                CatalogItem = new NodeCatalogItemDto
                {
                    Id = catalogItemId,
                    Name = catalogItemName,
                    Article = catalogItemArticle,
                    Barcode = catalogItemBarcode
                }
            };

        var shortages = new List<ItemDifferenceDto>();
        var surpluses = new List<ItemDifferenceDto>();

        foreach (var charId in allIds)
        {
            var decl = declaredByCharId.GetValueOrDefault(charId);
            var proc = processedByCharId.GetValueOrDefault(charId);

            var declCount = decl?.Count ?? 0;
            var procCount = proc?.Count ?? 0;

            if (declCount == procCount) continue;

            var src = decl ?? proc!;
            var charDto = BuildCharDto(charId, src.Characteristic, src.Barcode,
                src.CatalogItemId, src.CatalogItemName, src.CatalogItemArticle, src.CatalogItemBarcode);

            var diff = new ItemDifferenceDto
            {
                CatalogItemWithCharacteristic = charDto,
                DeclaredCount = declCount,
                ProcessedCount = procCount,
                DifferenceCount = Math.Abs(declCount - procCount)
            };

            if (declCount > procCount)
                shortages.Add(diff);
            else
                surpluses.Add(diff);
        }

        var result = new InboundOrderItemsComparisonDto
        {
            DeclaredItems = declared.Select(d => new ComparisonItemDto
            {
                CatalogItemWithCharacteristic = BuildCharDto(d.CatalogItemWithCharacteristicId,
                    d.Characteristic, d.Barcode, d.CatalogItemId, d.CatalogItemName, d.CatalogItemArticle, d.CatalogItemBarcode),
                Count = d.Count
            }).ToList(),
            ProcessedItems = processed.Select(p => new ComparisonItemDto
            {
                CatalogItemWithCharacteristic = BuildCharDto(p.CatalogItemWithCharacteristicId,
                    p.Characteristic, p.Barcode, p.CatalogItemId, p.CatalogItemName, p.CatalogItemArticle, p.CatalogItemBarcode),
                Count = p.Count
            }).ToList(),
            Shortages = shortages,
            Surpluses = surpluses,
            TotalShortageCount = shortages.Sum(s => s.DifferenceCount),
            TotalSurplusCount = surpluses.Sum(s => s.DifferenceCount)
        };

        return Ok(result);
    }

    /// <summary>Transition order from Draft to Processing (validates and copies draft items to declared).</summary>
    [HttpPost("{id:guid}/change-status-to-processing")]
    [Authorize]
    [ProducesResponseType<InboundOrderDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> ChangeStatusToProcessing(Guid id, CancellationToken ct = default)
    {
        var (order, error) = await LoadOrderWithEditCheck(id, ct);
        if (error is not null) return error;

        if (order!.Status != InboundOrderStatus.Draft)
            return Conflict(ErrorCode.InboundOrderInvalidStatus, "Order must be in Draft status to start processing.");

        await db.Entry(order).Collection(o => o.DraftItemsGroups).LoadAsync(ct);
        await db.Entry(order).Collection(o => o.DeclaredItemsGroups).LoadAsync(ct);

        var indexed = order.DraftItemsGroups.OrderBy(g => g.Order).Select((g, i) => (g, i)).ToList();

        // Groups that need full auto-creation (no CatalogItem, no Characteristic, CreateNew=true)
        var createBoth = indexed
            .Where(x => x.g.CatalogItemId is null && x.g.CatalogItemWithCharacteristicId is null && x.g.CreateNew)
            .ToList();

        // Groups that need characteristic auto-creation (CatalogItem known, no Characteristic, CreateNew=true)
        var createCharOnly = indexed
            .Where(x => x.g.CatalogItemId.HasValue && x.g.CatalogItemWithCharacteristicId is null && x.g.CreateNew)
            .ToList();

        var validationErrors = new List<(string Field, ErrorCode Code, string Message, IReadOnlyDictionary<string, object>? Args)>();

        // Validate createBoth: article uniqueness + rootBarcode uniqueness
        if (createBoth.Count > 0)
        {
            var articlesToCheck = createBoth.Select(x => x.g.Article).Distinct().ToList();
            var existingArticles = (await db.CatalogItems
                .Where(c => articlesToCheck.Contains(c.Article))
                .Select(c => c.Article)
                .ToListAsync(ct)).ToHashSet(StringComparer.OrdinalIgnoreCase);

            // Pre-compute in-request duplicates so ALL instances get an error, not just second+
            var duplicateArticlesInRequest = createBoth
                .GroupBy(x => x.g.Article, StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var rootBarcodesToCheck = createBoth
                .Where(x => x.g.RootBarcode is not null)
                .Select(x => x.g.RootBarcode!)
                .Distinct()
                .ToList();
            HashSet<string> existingRootBarcodes = [];
            if (rootBarcodesToCheck.Count > 0)
                existingRootBarcodes = (await db.CatalogItems
                    .Where(c => rootBarcodesToCheck.Contains(c.Barcode!))
                    .Select(c => c.Barcode!)
                    .ToListAsync(ct)).ToHashSet(StringComparer.OrdinalIgnoreCase);

            var duplicateRootBarcodesInRequest = createBoth
                .Where(x => x.g.RootBarcode is not null)
                .GroupBy(x => x.g.RootBarcode!, StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var (g, i) in createBoth)
            {
                if (duplicateArticlesInRequest.Contains(g.Article) || existingArticles.Contains(g.Article))
                    validationErrors.Add(($"draftItemsGroups[{i}].article",
                        ErrorCode.CatalogItemArticleDuplicate,
                        $"A catalog item with article '{g.Article}' already exists.", null));

                if (g.RootBarcode is not null &&
                    (duplicateRootBarcodesInRequest.Contains(g.RootBarcode) || existingRootBarcodes.Contains(g.RootBarcode)))
                    validationErrors.Add(($"draftItemsGroups[{i}].rootBarcode",
                        ErrorCode.CatalogItemBarcodeDuplicate,
                        $"A catalog item with barcode '{g.RootBarcode}' already exists.", null));
            }
        }

        // Validate createCharOnly: CatalogItem exists + characteristic uniqueness within item
        if (createCharOnly.Count > 0)
        {
            var neededCatalogIds = createCharOnly.Select(x => x.g.CatalogItemId!.Value).Distinct().ToList();
            var loadedCatalogItems = await db.CatalogItems
                .Include(c => c.Characteristics)
                .Where(c => neededCatalogIds.Contains(c.Id))
                .ToListAsync(ct);
            var catalogItemById = loadedCatalogItems.ToDictionary(c => c.Id);

            // Also check characteristic barcodes globally
            var charBarcodesToCheck = createCharOnly
                .Where(x => x.g.Barcode is not null)
                .Select(x => x.g.Barcode!)
                .Distinct()
                .ToList();
            HashSet<string> existingCharBarcodes = [];
            if (charBarcodesToCheck.Count > 0)
                existingCharBarcodes = (await db.CatalogItemsWithCharacteristics
                    .Where(c => charBarcodesToCheck.Contains(c.Barcode!))
                    .Select(c => c.Barcode!)
                    .ToListAsync(ct)).ToHashSet(StringComparer.OrdinalIgnoreCase);

            var seenCharBarcodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var (g, i) in createCharOnly)
            {
                if (!catalogItemById.TryGetValue(g.CatalogItemId!.Value, out var catalogItem))
                {
                    validationErrors.Add(($"draftItemsGroups[{i}].catalogItemId",
                        ErrorCode.CatalogItemNotFound, "Catalog item not found.", null));
                    continue;
                }

                if (catalogItem.Characteristics.Any(c =>
                        string.Equals(c.Characteristic, g.Characteristic, StringComparison.OrdinalIgnoreCase)))
                    validationErrors.Add(($"draftItemsGroups[{i}].characteristic",
                        ErrorCode.CatalogItemCharacteristicDuplicate,
                        $"Characteristic '{g.Characteristic}' already exists for this catalog item.", null));

                if (g.Barcode is not null)
                {
                    if (!seenCharBarcodes.Add(g.Barcode) || existingCharBarcodes.Contains(g.Barcode))
                        validationErrors.Add(($"draftItemsGroups[{i}].barcode",
                            ErrorCode.CatalogItemCharacteristicBarcodeDuplicate,
                            $"A characteristic with barcode '{g.Barcode}' already exists.", null));
                }
            }
        }

        // Items still missing a catalog link and not using CreateNew
        var missingLink = indexed
            .Where(x => x.g.CatalogItemWithCharacteristicId is null && !x.g.CreateNew)
            .ToList();
        foreach (var (_, i) in missingLink)
            validationErrors.Add(($"draftItemsGroups[{i}].catalogItemWithCharacteristicId",
                ErrorCode.InboundOrderDraftItemsMissingCatalogLink,
                "Draft item must be linked to a catalog item with characteristic.", null));

        if (validationErrors.Count > 0)
            return Problem(AppProblems.UnprocessableEntitiesWithRoot(
                ErrorCode.InboundOrderDraftItemsValidationFailed,
                "Draft items validation failed. Please fix all errors before starting processing.",
                validationErrors));

        // Apply auto-creation
        foreach (var (g, _) in createBoth)
        {
            var newItem = new CatalogItem
            {
                Id = Guid.NewGuid(),
                Name = g.Name,
                Article = g.Article,
                Barcode = g.RootBarcode
            };
            var newChar = new CatalogItemWithCharacteristic
            {
                Id = Guid.NewGuid(),
                Characteristic = g.Characteristic,
                Barcode = g.Barcode,
                CatalogItemId = newItem.Id
            };
            db.CatalogItems.Add(newItem);
            db.CatalogItemsWithCharacteristics.Add(newChar);
            g.CatalogItemId = newItem.Id;
            g.CatalogItemWithCharacteristicId = newChar.Id;
        }

        foreach (var (g, _) in createCharOnly)
        {
            var newChar = new CatalogItemWithCharacteristic
            {
                Id = Guid.NewGuid(),
                Characteristic = g.Characteristic,
                Barcode = g.Barcode,
                CatalogItemId = g.CatalogItemId!.Value
            };
            db.CatalogItemsWithCharacteristics.Add(newChar);
            g.CatalogItemWithCharacteristicId = newChar.Id;
        }

        db.InboundOrderDeclaredItemsGroups.RemoveRange(order.DeclaredItemsGroups);

        foreach (var draft in order.DraftItemsGroups)
        {
            db.InboundOrderDeclaredItemsGroups.Add(new InboundOrderDeclaredItemsGroup
            {
                Id = Guid.NewGuid(),
                InboundOrderId = id,
                CatalogItemWithCharacteristicId = draft.CatalogItemWithCharacteristicId!.Value,
                Count = draft.Count
            });
        }

        var beforeDto = await db.InboundOrders
            .ProjectTo<InboundOrderDto>(mapper.ConfigurationProvider)
            .FirstAsync(o => o.Id == id, ct);

        order.Status = InboundOrderStatus.Processing;
        await db.SaveChangesAsync(ct);

        var afterDto = await db.InboundOrders
            .ProjectTo<InboundOrderDto>(mapper.ConfigurationProvider)
            .FirstAsync(o => o.Id == id, ct);

        await changeLog.CompareAndSaveToChangelog(beforeDto, afterDto, action: "changeStatus");

        return Ok(afterDto);
    }

    /// <summary>Rollback order from Processing to Draft (only if no processed items exist).</summary>
    [HttpPost("{id:guid}/rollback-status-to-draft")]
    [Authorize]
    [ProducesResponseType<InboundOrderDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RollbackStatusToDraft(Guid id, CancellationToken ct = default)
    {
        var (order, error) = await LoadOrderWithEditCheck(id, ct);
        if (error is not null) return error;

        if (order!.Status != InboundOrderStatus.Processing)
            return Conflict(ErrorCode.InboundOrderInvalidStatus, "Order must be in Processing status to rollback to Draft.");

        await db.Entry(order).Collection(o => o.ProcessedItemsGroups).LoadAsync(ct);
        await db.Entry(order).Collection(o => o.DeclaredItemsGroups).LoadAsync(ct);

        if (order.ProcessedItemsGroups.Count > 0)
            return Conflict(ErrorCode.InboundOrderInvalidStatus, "Cannot rollback to Draft: processed items exist.");

        var beforeDto = await db.InboundOrders
            .ProjectTo<InboundOrderDto>(mapper.ConfigurationProvider)
            .FirstAsync(o => o.Id == id, ct);

        db.InboundOrderDeclaredItemsGroups.RemoveRange(order.DeclaredItemsGroups);
        order.Status = InboundOrderStatus.Draft;
        await db.SaveChangesAsync(ct);

        var afterDto = await db.InboundOrders
            .ProjectTo<InboundOrderDto>(mapper.ConfigurationProvider)
            .FirstAsync(o => o.Id == id, ct);

        await changeLog.CompareAndSaveToChangelog(beforeDto, afterDto, action: "changeStatus");

        return Ok(afterDto);
    }

    /// <summary>Transition order from Processing to Finished.</summary>
    [HttpPost("{id:guid}/change-status-to-finished")]
    [Authorize]
    [ProducesResponseType<InboundOrderDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ChangeStatusToFinished(Guid id, CancellationToken ct = default)
    {
        var (order, error) = await LoadOrderWithEditCheck(id, ct);
        if (error is not null) return error;

        if (order!.Status != InboundOrderStatus.Processing)
            return Conflict(ErrorCode.InboundOrderInvalidStatus, "Order must be in Processing status to finish.");

        var beforeDto = await db.InboundOrders
            .ProjectTo<InboundOrderDto>(mapper.ConfigurationProvider)
            .FirstAsync(o => o.Id == id, ct);

        order.Status = InboundOrderStatus.Finished;
        await db.SaveChangesAsync(ct);

        var afterDto = await db.InboundOrders
            .ProjectTo<InboundOrderDto>(mapper.ConfigurationProvider)
            .FirstAsync(o => o.Id == id, ct);

        await changeLog.CompareAndSaveToChangelog(beforeDto, afterDto, action: "changeStatus");

        return Ok(afterDto);
    }

    /// <summary>Rollback order from Finished to Processing.</summary>
    [HttpPost("{id:guid}/rollback-status-to-processing")]
    [Authorize]
    [ProducesResponseType<InboundOrderDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RollbackStatusToProcessing(Guid id, CancellationToken ct = default)
    {
        var (order, error) = await LoadOrderWithEditCheck(id, ct);
        if (error is not null) return error;

        if (order!.Status != InboundOrderStatus.Finished)
            return Conflict(ErrorCode.InboundOrderInvalidStatus, "Order must be in Finished status to rollback to Processing.");

        var beforeDto = await db.InboundOrders
            .ProjectTo<InboundOrderDto>(mapper.ConfigurationProvider)
            .FirstAsync(o => o.Id == id, ct);

        order.Status = InboundOrderStatus.Processing;
        await db.SaveChangesAsync(ct);

        var afterDto = await db.InboundOrders
            .ProjectTo<InboundOrderDto>(mapper.ConfigurationProvider)
            .FirstAsync(o => o.Id == id, ct);

        await changeLog.CompareAndSaveToChangelog(beforeDto, afterDto, action: "changeStatus");

        return Ok(afterDto);
    }

    /// <summary>Try to auto-assign CatalogItemWithCharacteristic to draft items by matching barcode → article+characteristic → name+characteristic.</summary>
    [HttpPost("{id:guid}/try-auto-assign-catalog-items")]
    [Authorize]
    [ProducesResponseType<IReadOnlyList<InboundOrderDraftItemsGroupDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> TryAutoAssignCatalogItems(
        Guid id,
        [FromBody] TryAutoAssignRequest request,
        CancellationToken ct = default)
    {
        var canEditAll = User.HasClaim("permission", Permissions.InboundOrders.Edit);
        var canEditAssigned = User.HasClaim("permission", Permissions.InboundOrders.EditAssignedWarehouses);

        if (!canEditAll && !canEditAssigned)
            return Forbidden();

        var order = await db.InboundOrders
            .Include(o => o.DraftItemsGroups)
            .FirstOrDefaultAsync(o => o.Id == id, ct);

        if (order is null)
            return NotFound(ErrorCode.InboundOrderNotFound, "Inbound order not found.");

        if (!canEditAll)
        {
            var assignedIds = await GetCurrentUserAssignedWarehouseIdsAsync(db, ct);
            if (assignedIds is null)
                return Unauthorized(ErrorCode.TokenInvalid, "Invalid token.");
            if (!assignedIds.Contains(order.WarehouseId))
                return Forbidden();
        }

        if (order.Status != InboundOrderStatus.Draft)
            return Conflict(ErrorCode.InboundOrderInvalidStatus, "Auto-assign is only available for orders in Draft status.");

        var targetGroups = request.DraftItemsGroupIds.Count > 0
            ? order.DraftItemsGroups
                .Where(g => request.DraftItemsGroupIds.Contains(g.Id) && g.CatalogItemWithCharacteristicId is null)
                .ToList()
            : order.DraftItemsGroups
                .Where(g => g.CatalogItemWithCharacteristicId is null)
                .ToList();

        if (targetGroups.Count == 0)
        {
            var groups = await db.InboundOrderDraftItemsGroups
                .Where(g => g.InboundOrderId == id)
                .ProjectTo<InboundOrderDraftItemsGroupDto>(mapper.ConfigurationProvider)
                .ToListAsync(ct);
            return Ok(groups);
        }

        // Collect candidate data
        var barcodes = targetGroups.Where(g => g.Barcode is not null).Select(g => g.Barcode!).ToHashSet();
        var articles = targetGroups.Select(g => g.Article).ToHashSet();
        var names = targetGroups.Select(g => g.Name).ToHashSet();

        // Single bulk query: load all potentially matching characteristics
        var candidates = await db.CatalogItemsWithCharacteristics
            .Include(c => c.CatalogItem)
            .Where(c =>
                (c.Barcode != null && barcodes.Contains(c.Barcode)) ||
                articles.Contains(c.CatalogItem.Article) ||
                names.Contains(c.CatalogItem.Name))
            .ToListAsync(ct);

        var byBarcode = candidates
            .Where(c => c.Barcode is not null)
            .GroupBy(c => c.Barcode!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        var byArticleAndChar = candidates
            .GroupBy(c => (c.CatalogItem.Article.ToLowerInvariant(), c.Characteristic.ToLowerInvariant()))
            .ToDictionary(g => g.Key, g => g.First());
        var byNameAndChar = candidates
            .GroupBy(c => (c.CatalogItem.Name.ToLowerInvariant(), c.Characteristic.ToLowerInvariant()))
            .ToDictionary(g => g.Key, g => g.First());

        foreach (var g in targetGroups)
        {
            CatalogItemWithCharacteristic? match = null;

            if (g.Barcode is not null && byBarcode.TryGetValue(g.Barcode, out var byBarcodeMatch))
                match = byBarcodeMatch;
            else if (byArticleAndChar.TryGetValue((g.Article.ToLowerInvariant(), g.Characteristic.ToLowerInvariant()), out var byArticleMatch))
                match = byArticleMatch;
            else if (byNameAndChar.TryGetValue((g.Name.ToLowerInvariant(), g.Characteristic.ToLowerInvariant()), out var byNameMatch))
                match = byNameMatch;

            if (match is not null)
            {
                g.CatalogItemWithCharacteristicId = match.Id;
                g.CatalogItemId = match.CatalogItemId;
            }
        }

        await db.SaveChangesAsync(ct);

        var result = await db.InboundOrderDraftItemsGroups
            .Where(g => g.InboundOrderId == id)
            .OrderBy(g => g.Order)
            .ProjectTo<InboundOrderDraftItemsGroupDto>(mapper.ConfigurationProvider)
            .ToListAsync(ct);

        return Ok(result);
    }

    private async Task<(InboundOrder? Order, IActionResult? Error)> LoadOrderWithEditCheck(Guid id, CancellationToken ct)
    {
        var canEditAll = User.HasClaim("permission", Permissions.InboundOrders.Edit);
        var canEditAssigned = User.HasClaim("permission", Permissions.InboundOrders.EditAssignedWarehouses);

        if (!canEditAll && !canEditAssigned)
            return (null, Forbidden());

        var order = await db.InboundOrders.FirstOrDefaultAsync(o => o.Id == id, ct);
        if (order is null)
            return (null, NotFound(ErrorCode.InboundOrderNotFound, "Inbound order not found."));

        if (!canEditAll)
        {
            var assignedIds = await GetCurrentUserAssignedWarehouseIdsAsync(db, ct);
            if (assignedIds is null)
                return (null, Unauthorized(ErrorCode.TokenInvalid, "Invalid token."));
            if (!assignedIds.Contains(order.WarehouseId))
                return (null, Forbidden());
        }

        return (order, null);
    }
}
