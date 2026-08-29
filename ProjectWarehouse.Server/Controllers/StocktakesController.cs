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
using ProjectWarehouse.Server.Models.Catalog;
using ProjectWarehouse.Server.Models.Stocktakes;
using ProjectWarehouse.Server.Services;

namespace ProjectWarehouse.Server.Controllers;

[Route("api/stocktakes")]
public class StocktakesController(
    ApplicationDbContext db,
    IMapper mapper,
    IInventoryService inventory,
    IStocktakeDiffCalculator diffCalculator,
    EntityAccessRegistry access,
    IChangeLogService<StocktakeDto> changeLog) : AppControllerBase
{
    private EntityAccessRule<Stocktake> Rule => access.For<Stocktake>();

    // ── Helpers ───────────────────────────────────────────────────────────────

    private IQueryable<Stocktake> BaseQuery(bool includeItems = false)
    {
        var q = db.Stocktakes
            .Include(s => s.Warehouse)
            .AsQueryable();

        if (includeItems)
            q = q
                .Include(s => s.Nodes)
                .ThenInclude(n => n.StoragePlaceNode)
                .ThenInclude(n => n.RootStoragePlace)
                .Include(s => s.Nodes)
                .ThenInclude(n => n.Items)
                .ThenInclude(i => i.CatalogItem)
                .Include(s => s.Nodes)
                .ThenInclude(n => n.Items)
                .ThenInclude(i => i.UnitInventoryItem);
        else
            q = q.Include(s => s.Nodes);

        return q;
    }

    private async Task<(Stocktake? stocktake, IActionResult? error)> LoadStocktakeWithAccessAsync(
        Guid id, AccessLevel level, CancellationToken ct, bool includeItems = false)
    {
        if (AccessError(await Rule.PrecheckAsync(User, level, ct)) is { } prelude)
            return (null, prelude);

        var stocktake = await BaseQuery(includeItems).FirstOrDefaultAsync(s => s.Id == id, ct);
        if (stocktake is null)
            return (null, NotFound(ErrorCode.StocktakeNotFound, "Stocktake not found."));

        if (AccessError(await Rule.CheckAsync(User, level, stocktake, ct)) is { } denied)
            return (null, denied);

        return (stocktake, null);
    }

    private Task<(Stocktake? stocktake, IActionResult? error)> LoadStocktakeWithViewAccessAsync(
        Guid id, CancellationToken ct, bool includeItems = false) =>
        LoadStocktakeWithAccessAsync(id, AccessLevel.View, ct, includeItems);

    private Task<(Stocktake? stocktake, IActionResult? error)> LoadStocktakeWithEditAccessAsync(
        Guid id, CancellationToken ct, bool includeItems = false) =>
        LoadStocktakeWithAccessAsync(id, AccessLevel.Edit, ct, includeItems);

    private async Task<Dictionary<Guid, StoragePlaceNode>> LoadWarehouseNodesAsync(
        Guid warehouseId, CancellationToken ct) =>
        await db.StoragePlacesNodes
            .Where(n => n.RootStoragePlace.WarehouseId == warehouseId)
            .Include(n => n.RootStoragePlace)
            .ToDictionaryAsync(n => n.Id, ct);

    /// <summary>
    /// Two counts running over one cell would fight each other at finish — the second one to apply
    /// overwrites the first with quantities measured before it. Checked wherever a cell can end up
    /// in a running count: at start, and when the scope of an InProgress document grows.
    /// </summary>
    private async Task<IActionResult?> FindNodeCountedElsewhereAsync(
        Guid stocktakeId, IReadOnlyCollection<Guid> nodeIds, CancellationToken ct)
    {
        if (nodeIds.Count == 0) return null;

        var busy = await db.StocktakeNodes
            .Where(n => nodeIds.Contains(n.StoragePlaceNodeId)
                        && n.StocktakeId != stocktakeId
                        && n.Stocktake.Status == StocktakeStatus.InProgress)
            .OrderBy(n => n.StoragePlaceNode.Name)
            .Select(n => new { n.StoragePlaceNodeId, n.StoragePlaceNode.Name })
            .FirstOrDefaultAsync(ct);

        return busy is null
            ? null
            : UnprocessableEntity("root", ErrorCode.StocktakeNodeAlreadyInProgress,
                $"Storage node '{busy.Name}' is already being counted in another stocktake.",
                new Dictionary<string, object> { ["nodeId"] = busy.StoragePlaceNodeId });
    }

    private StocktakeDto MapWithNodes(Stocktake stocktake, Dictionary<Guid, StoragePlaceNode> nodeById) =>
        mapper.Map<StocktakeDto>(stocktake, opts => opts.Items["nodeById"] = nodeById);

    private async Task<StocktakeDto> BuildDtoAsync(Stocktake stocktake, CancellationToken ct)
    {
        var nodeById = await LoadWarehouseNodesAsync(stocktake.WarehouseId, ct);
        return MapWithNodes(stocktake, nodeById);
    }

    private async Task<StocktakeDto> ReloadDtoAsync(Guid id, CancellationToken ct)
    {
        var reloaded = await BaseQuery(includeItems: true).FirstAsync(s => s.Id == id, ct);
        return await BuildDtoAsync(reloaded, ct);
    }

    // ── GET list ──────────────────────────────────────────────────────────────

    /// <summary>List stocktakes with pagination, filtering, and search.</summary>
    /// <remarks>
    /// Query params: <c>page</c> (default 1), <c>pageSize</c> (default 20, max 200), <c>searchString</c>,
    /// <c>warehouseId</c>, <c>status</c>, <c>sortBy</c> (default <c>Number</c>), <c>sortOrder</c>
    /// (default <c>Desc</c>).
    /// Requires <c>stocktakes.view</c> or <c>stocktakes.view_assigned</c>; without either, 403
    /// <c>permissionDenied</c>. 401 <c>tokenInvalid</c> when an <c>_assigned</c> permission is used but the
    /// token carries no resolvable user.
    /// </remarks>
    [HttpGet]
    [Authorize]
    [ProducesResponseType<Paginated<StocktakeSummaryDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(
        [FromQuery][Range(1, int.MaxValue)] int page = 1,
        [FromQuery][Range(1, 200)] int pageSize = 20,
        [FromQuery] string? searchString = null,
        [FromQuery] Guid? warehouseId = null,
        [FromQuery] StocktakeStatus? status = null,
        [FromQuery] StocktakeSortBy sortBy = StocktakeSortBy.Number,
        [FromQuery] SortOrder sortOrder = SortOrder.Desc,
        CancellationToken ct = default)
    {
        if (AccessError(await Rule.PrecheckAsync(User, AccessLevel.View, ct)) is { } error)
            return error;

        var accessible = await Rule.QueryAsync(User, AccessLevel.View, ct);

        var baseQuery = accessible
            .Where(s => warehouseId == null || s.WarehouseId == warehouseId)
            .Where(s => status == null || s.Status == status)
            .WhereMatchesSearch(s => s.SearchString, searchString);

        var query = sortBy switch
        {
            // Planned is numbered last in the enum to keep stored values stable, so order it explicitly
            StocktakeSortBy.Status => baseQuery
                .Sort(s => s.Status == StocktakeStatus.Planned    ? 0
                         : s.Status == StocktakeStatus.Draft      ? 1
                         : s.Status == StocktakeStatus.InProgress ? 2
                         : s.Status == StocktakeStatus.Finished   ? 3
                         : 4, sortOrder)
                .ThenBy(s => s.Id),
            StocktakeSortBy.CreatedAt     => baseQuery.Sort(s => s.CreatedAt, sortOrder).ThenBy(s => s.Id),
            StocktakeSortBy.WarehouseName => baseQuery.Sort(s => s.Warehouse.Name, sortOrder).ThenBy(s => s.Id),
            StocktakeSortBy.Name          => baseQuery.Sort(s => s.Name, sortOrder).ThenBy(s => s.Id),
            _                             => baseQuery.Sort(s => s.Number, sortOrder).ThenBy(s => s.Id),
        };

        var paginated = await query
            .ProjectTo<StocktakeSummaryDto>(mapper.ConfigurationProvider)
            .ToPaginatedAsync(page, pageSize, ct);

        return Ok(paginated);
    }

    // ── GET single ────────────────────────────────────────────────────────────

    /// <summary>Get full stocktake details including counted nodes and their items.</summary>
    /// <remarks>
    /// Errors: 404 <c>stocktakeNotFound</c>; 403 <c>permissionDenied</c> without a view permission, or 403
    /// <c>stocktakeNotAssignedToWarehouse</c> when only <c>stocktakes.view_assigned</c> is held and the
    /// document belongs to another warehouse; 401 <c>tokenInvalid</c> for an unresolvable user.
    /// </remarks>
    [HttpGet("{id:guid}")]
    [Authorize]
    [ProducesResponseType<StocktakeDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct = default)
    {
        var (stocktake, error) = await LoadStocktakeWithViewAccessAsync(id, ct, includeItems: true);
        if (error is not null) return error;

        return Ok(await BuildDtoAsync(stocktake!, ct));
    }

    // ── POST create ───────────────────────────────────────────────────────────

    /// <summary>Create a new stocktake. Always starts in Draft status.</summary>
    /// <remarks>
    /// Body: <c>CreateStocktakeRequest</c> — warehouseId (required), name, notes, type, plannedDate.
    /// Errors: 422 <c>warehouseNotFound</c> for an unknown warehouse; 422 <c>validationError</c> on
    /// <c>plannedDate</c> when <c>type</c> is <c>Scheduled</c> and no date is given; 403
    /// <c>permissionDenied</c> without <c>stocktakes.edit</c>/<c>stocktakes.edit_assigned</c>, or 403
    /// <c>stocktakeNotAssignedToWarehouse</c> when only the <c>_assigned</c> permission is held and the target
    /// warehouse is not assigned.
    /// </remarks>
    [HttpPost]
    [Authorize]
    [ProducesResponseType<StocktakeDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Create([FromBody] CreateStocktakeRequest request, CancellationToken ct = default)
    {
        if (AccessError(await Rule.PrecheckAsync(User, AccessLevel.Edit, ct)) is { } error)
            return error;

        var warehouse = await db.Warehouses.FindAsync([request.WarehouseId], ct);
        if (warehouse is null)
            return UnprocessableEntity("warehouseId", ErrorCode.WarehouseNotFound, "Warehouse not found.");

        if (AccessError(await Rule.CheckWarehouseAsync(User, AccessLevel.Edit, request.WarehouseId, ct)) is { } denied)
            return denied;

        var isScheduled = request.Type == StocktakeType.Scheduled;

        if (isScheduled && request.PlannedDate is null)
            return UnprocessableEntity("plannedDate", ErrorCode.ValidationError,
                "Planned date is required for a scheduled stocktake.");

        var stocktake = new Stocktake
        {
            Id          = Guid.NewGuid(),
            Name        = request.Name,
            Notes       = request.Notes,
            WarehouseId = request.WarehouseId,
            CreatedById = GetCurrentUserId(),
            CreatedAt   = DateTime.UtcNow,
            Status      = StocktakeStatus.Draft,
            Type        = request.Type,
            PlannedDate = isScheduled ? request.PlannedDate : null,
        };

        db.Stocktakes.Add(stocktake);
        await db.SaveChangesAsync(ct);

        await db.Entry(stocktake).Reference(s => s.Warehouse).LoadAsync(ct);

        var dto = mapper.Map<StocktakeDto>(stocktake);
        await changeLog.CompareAndSaveToChangelog(null, dto);

        return CreatedAtAction(nameof(GetById), new { id = stocktake.Id }, dto);
    }

    // ── PATCH update ──────────────────────────────────────────────────────────

    /// <summary>
    /// Update stocktake name, notes, type and planned date. Allowed while the document is still open;
    /// type and planned date freeze once counting has started.
    /// </summary>
    /// <remarks>
    /// Planned, Draft or InProgress only. Errors:
    /// <list type="bullet">
    ///   <item>404 <c>stocktakeNotFound</c></item>
    ///   <item>422 <c>stocktakeInvalidStatusTransition</c> — the document is Finished or Canceled, or
    ///     <c>type</c>/<c>plannedDate</c> is sent while it is InProgress (field <c>type</c>)</item>
    ///   <item>422 <c>validationError</c> — <c>plannedDate</c> sent without <c>type</c>, or <c>type</c> is
    ///     <c>Scheduled</c> with no <c>plannedDate</c></item>
    ///   <item>403 <c>permissionDenied</c> / <c>stocktakeNotAssignedToWarehouse</c> (edit access)</item>
    /// </list>
    /// </remarks>
    [HttpPatch("{id:guid}")]
    [Authorize]
    [ProducesResponseType<StocktakeDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateStocktakeRequest request,
        CancellationToken ct = default)
    {
        var (stocktake, error) = await LoadStocktakeWithEditAccessAsync(id, ct, includeItems: true);
        if (error is not null) return error;

        if (stocktake!.Status is not (StocktakeStatus.Planned or StocktakeStatus.Draft or StocktakeStatus.InProgress))
            return UnprocessableEntity("root", ErrorCode.StocktakeInvalidStatusTransition,
                "Stocktake can only be updated while it is open.");

        var sendsPlanning = request.Type is not null || request.PlannedDate is not null;

        if (sendsPlanning && stocktake.Status is not (StocktakeStatus.Planned or StocktakeStatus.Draft))
            return UnprocessableEntity("type", ErrorCode.StocktakeInvalidStatusTransition,
                "Planning can only be changed while the stocktake is Planned or Draft.");

        // plannedDate belongs to type — patching it alone would leave the pair ambiguous
        if (sendsPlanning && request.Type is null)
            return UnprocessableEntity("type", ErrorCode.ValidationError,
                "Planned date cannot be changed without sending the type.");

        if (sendsPlanning && request.Type == StocktakeType.Scheduled && request.PlannedDate is null)
            return UnprocessableEntity("plannedDate", ErrorCode.ValidationError,
                "Planned date is required for a scheduled stocktake.");

        var before = await BuildDtoAsync(stocktake, ct);

        stocktake.Name  = request.Name;
        stocktake.Notes = request.Notes;

        if (sendsPlanning)
        {
            stocktake.Type        = request.Type!.Value;
            stocktake.PlannedDate = request.Type == StocktakeType.Scheduled ? request.PlannedDate : null;

            // Planned only makes sense for a scheduled document
            if (stocktake.Status == StocktakeStatus.Planned && request.Type != StocktakeType.Scheduled)
                stocktake.Status = StocktakeStatus.Draft;
        }

        await db.SaveChangesAsync(ct);

        var after = await BuildDtoAsync(stocktake, ct);
        await changeLog.CompareAndSaveToChangelog(before, after);

        return Ok(after);
    }

    // ── DELETE ────────────────────────────────────────────────────────────────

    /// <summary>Delete a stocktake. Only allowed in Planned or Draft status.</summary>
    /// <remarks>
    /// Errors: 404 <c>stocktakeNotFound</c>; 422 <c>stocktakeInvalidStatusTransition</c> outside Planned or
    /// Draft; 403 <c>permissionDenied</c> / <c>stocktakeNotAssignedToWarehouse</c> (edit access).
    /// </remarks>
    [HttpDelete("{id:guid}")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct = default)
    {
        var (stocktake, error) = await LoadStocktakeWithEditAccessAsync(id, ct, includeItems: true);
        if (error is not null) return error;

        if (stocktake!.Status is not (StocktakeStatus.Planned or StocktakeStatus.Draft))
            return UnprocessableEntity("root", ErrorCode.StocktakeInvalidStatusTransition,
                "Only Planned or Draft stocktakes can be deleted.");

        var dto = await BuildDtoAsync(stocktake, ct);

        db.Stocktakes.Remove(stocktake);
        await db.SaveChangesAsync(ct);

        await changeLog.CompareAndSaveToChangelog(dto, null);

        return NoContent();
    }

    // ── PUT nodes sync ────────────────────────────────────────────────────────

    /// <summary>
    /// Replace the set of counted cells. Cells already in scope keep their counted items; dropping a
    /// cell discards its lines.
    /// </summary>
    /// <remarks>
    /// Planned, Draft or InProgress only. Errors:
    /// <list type="bullet">
    ///   <item>404 <c>stocktakeNotFound</c></item>
    ///   <item>422 <c>stocktakeInvalidStatusTransition</c> — the document is Finished or Canceled</item>
    ///   <item>422 <c>validationError</c> — a node id repeated in <c>nodeIds</c></item>
    ///   <item>422 <c>storagePlaceNodeNotFound</c> — a node does not belong to this warehouse
    ///     (field <c>nodeIds[i]</c>)</item>
    ///   <item>422 <c>stocktakeNodeAlreadyInProgress</c> — only while InProgress, when a newly added cell is
    ///     already being counted in another InProgress stocktake; <c>args: { nodeId }</c>. A cell may sit in
    ///     any number of Draft or Planned scopes</item>
    ///   <item>403 <c>permissionDenied</c> / <c>stocktakeNotAssignedToWarehouse</c> (edit access)</item>
    /// </list>
    /// </remarks>
    [HttpPut("{id:guid}/nodes")]
    [Authorize]
    [ProducesResponseType<StocktakeDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> SyncNodes(Guid id, [FromBody] SyncStocktakeNodesRequest request,
        CancellationToken ct = default)
    {
        var (stocktake, error) = await LoadStocktakeWithEditAccessAsync(id, ct, includeItems: true);
        if (error is not null) return error;

        if (stocktake!.Status is not (StocktakeStatus.Planned or StocktakeStatus.Draft or StocktakeStatus.InProgress))
            return UnprocessableEntity("root", ErrorCode.StocktakeInvalidStatusTransition,
                "Scope can only be changed while the stocktake is open.");

        var requestedIds = request.NodeIds.ToList();

        var duplicates = requestedIds.GroupBy(x => x).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        if (duplicates.Count > 0)
            return UnprocessableEntity("nodeIds", ErrorCode.ValidationError, "Duplicate storage nodes in request.");

        var validIds = await db.StoragePlacesNodes
            .Where(n => requestedIds.Contains(n.Id) && n.RootStoragePlace.WarehouseId == stocktake.WarehouseId)
            .Select(n => n.Id)
            .ToListAsync(ct);

        for (var i = 0; i < requestedIds.Count; i++)
            if (!validIds.Contains(requestedIds[i]))
                return UnprocessableEntity($"nodeIds[{i}]", ErrorCode.StoragePlaceNodeNotFound,
                    $"Storage node '{requestedIds[i]}' not found in this warehouse.");

        // A cell may sit in several drafts at once — only two running counts clash
        var addedIds = requestedIds.Except(stocktake.Nodes.Select(n => n.StoragePlaceNodeId)).ToList();

        if (stocktake.Status == StocktakeStatus.InProgress)
        {
            var conflict = await FindNodeCountedElsewhereAsync(id, addedIds, ct);
            if (conflict is not null) return conflict;
        }

        var before = await BuildDtoAsync(stocktake, ct);

        var removed = stocktake.Nodes.Where(n => !requestedIds.Contains(n.StoragePlaceNodeId)).ToList();
        db.StocktakeNodes.RemoveRange(removed);
        foreach (var node in removed) stocktake.Nodes.Remove(node);

        foreach (var nodeId in addedIds)
            db.StocktakeNodes.Add(new StocktakeNode
            {
                Id                 = Guid.NewGuid(),
                StocktakeId        = stocktake.Id,
                StoragePlaceNodeId = nodeId,
            });

        await db.SaveChangesAsync(ct);

        var after = await ReloadDtoAsync(id, ct);
        await changeLog.CompareAndSaveToChangelog(before, after, StocktakeActions.NodesSynced);

        return Ok(after);
    }

    // ── GET node stock ────────────────────────────────────────────────────────

    /// <summary>
    /// Live stock of one cell in the scope, used to pre-populate the counting screen. Served here
    /// rather than through the inventory endpoints so counting does not require warehouse permissions.
    /// </summary>
    /// <remarks>
    /// Errors: 404 <c>stocktakeNotFound</c>; 404 <c>stocktakeNodeNotFound</c> when the cell is not in this
    /// document's scope; 403 <c>permissionDenied</c> / <c>stocktakeNotAssignedToWarehouse</c> (view access);
    /// 401 <c>tokenInvalid</c>.
    /// </remarks>
    [HttpGet("{id:guid}/nodes/{nodeId:guid}/stock")]
    [Authorize]
    [ProducesResponseType<StocktakeNodeStockDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetNodeStock(Guid id, Guid nodeId, CancellationToken ct = default)
    {
        var (stocktake, error) = await LoadStocktakeWithViewAccessAsync(id, ct);
        if (error is not null) return error;

        if (stocktake!.Nodes.All(n => n.StoragePlaceNodeId != nodeId))
            return NotFound(ErrorCode.StocktakeNodeNotFound, "Storage node is not part of this stocktake.");

        var groups = await db.StoragePlacesNodesItemsGroups
            .Where(g => g.StoragePlaceNodeId == nodeId && g.Count > 0)
            .Include(g => g.CatalogItem)
            .ToListAsync(ct);

        var standard = groups
            .Select(g => new StocktakeNodeStandardStockDto
            {
                CatalogItemId   = g.CatalogItemId,
                CatalogItem     = mapper.Map<CatalogItemSummaryDto>(g.CatalogItem),
                CatalogItemName = g.CatalogItem.Name,
                Expected        = g.Count,
            })
            .ToList();

        var unitItems = await db.InventoryItems.OfType<UnitInventoryItem>()
            .Where(u => u.StoragePlaceNodeId == nodeId)
            .Include(u => u.CatalogItem)
            .ToListAsync(ct);

        var units = unitItems
            .Select(u => new StocktakeNodeUnitStockDto
            {
                UnitInventoryItemId = u.Id,
                InventoryNumber     = u.InventoryNumber,
                CatalogItemId       = u.CatalogItemId,
                CatalogItem         = mapper.Map<CatalogItemSummaryDto>(u.CatalogItem),
                CatalogItemName     = u.CatalogItem.Name,
            })
            .ToList();

        var nodeById = await LoadWarehouseNodesAsync(stocktake.WarehouseId, ct);

        return Ok(new StocktakeNodeStockDto
        {
            StoragePlaceNodeId = nodeId,
            NodePath = nodeById.TryGetValue(nodeId, out var node)
                ? StoragePlaceNodeHelper.BuildPath(node, nodeById)
                : [],
            Standard = standard,
            Units    = units,
        });
    }

    // ── PUT items sync ────────────────────────────────────────────────────────

    /// <summary>
    /// Replace the counted lines of one cell. Scoped to a single cell so accordions save independently.
    /// </summary>
    /// <remarks>
    /// InProgress status only. Errors:
    /// <list type="bullet">
    ///   <item>404 <c>stocktakeNotFound</c>; 404 <c>stocktakeNodeNotFound</c> when the cell is not in scope</item>
    ///   <item>422 <c>stocktakeInvalidStatusTransition</c> — the document is not InProgress</item>
    ///   <item>422 <c>catalogItemNotFound</c> — unknown <c>catalogItemId</c> (field <c>items[i].catalogItemId</c>)</item>
    ///   <item>422 <c>validationError</c> — line kind does not match the catalog item type, a standard line
    ///     carries an inventory number, or a standard item / inventory number is repeated in the request</item>
    ///   <item>422 <c>outOfRange</c> — negative standard quantity, or a unit quantity outside 0..1</item>
    ///   <item>422 <c>required</c> — a unit line without an inventory number</item>
    ///   <item>422 <c>stocktakeUnitItemInAnotherWarehouse</c> — a claimed serial is booked in another
    ///     warehouse; <c>args: { inventoryNumber }</c>. Takes precedence over the clash below</item>
    ///   <item>422 <c>stocktakeUnitCountedTwice</c> — the same serial is claimed found in another InProgress
    ///     stocktake (<c>args: { inventoryNumber, stocktakeId, stocktakeNumber }</c>) or in another cell of
    ///     this document (<c>args: { inventoryNumber }</c>). Surpluses count too</item>
    ///   <item>403 <c>permissionDenied</c> / <c>stocktakeNotAssignedToWarehouse</c> (edit access)</item>
    /// </list>
    /// </remarks>
    [HttpPut("{id:guid}/nodes/{nodeId:guid}/items")]
    [Authorize]
    [ProducesResponseType<StocktakeDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> SyncNodeItems(Guid id, Guid nodeId,
        [FromBody] IReadOnlyList<StocktakeItemRequest> items, CancellationToken ct = default)
    {
        var (stocktake, error) = await LoadStocktakeWithEditAccessAsync(id, ct, includeItems: true);
        if (error is not null) return error;

        if (stocktake!.Status != StocktakeStatus.InProgress)
            return UnprocessableEntity("root", ErrorCode.StocktakeInvalidStatusTransition,
                "Counted items can only be edited while the stocktake is in progress.");

        var scopeNode = stocktake.Nodes.FirstOrDefault(n => n.StoragePlaceNodeId == nodeId);
        if (scopeNode is null)
            return NotFound(ErrorCode.StocktakeNodeNotFound, "Storage node is not part of this stocktake.");

        var validation = await ValidateNodeItemsAsync(stocktake, nodeId, items, ct);
        if (validation is not null) return validation;

        var before = await BuildDtoAsync(stocktake, ct);

        db.StocktakeItems.RemoveRange(scopeNode.Items);
        scopeNode.Items.Clear();

        foreach (var req in items)
        {
            var number = req.InventoryNumber?.Trim();
            var unitId = req.Kind == StocktakeItemKind.Unit
                ? await ResolveUnitIdAsync(req.CatalogItemId, number!, ct)
                : null;

            db.StocktakeItems.Add(new StocktakeItem
            {
                Id                  = Guid.NewGuid(),
                StocktakeNodeId     = scopeNode.Id,
                Kind                = req.Kind,
                CatalogItemId       = req.CatalogItemId,
                CountedQuantity     = req.CountedQuantity,
                InventoryNumber     = req.Kind == StocktakeItemKind.Unit ? number : null,
                UnitInventoryItemId = unitId,
                Notes               = req.Notes,
            });
        }

        await db.SaveChangesAsync(ct);

        var after = await ReloadDtoAsync(id, ct);
        await changeLog.CompareAndSaveToChangelog(before, after, StocktakeActions.ItemsSynced);

        return Ok(after);
    }

    private async Task<IActionResult?> ValidateNodeItemsAsync(
        Stocktake stocktake, Guid nodeId, IReadOnlyList<StocktakeItemRequest> items, CancellationToken ct)
    {
        var catalogIds = items.Select(i => i.CatalogItemId).Distinct().ToList();
        var catalogTypes = await db.CatalogItems
            .Where(c => catalogIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c.Type, ct);

        for (var i = 0; i < items.Count; i++)
        {
            var req = items[i];
            var prefix = $"items[{i}]";

            if (!catalogTypes.TryGetValue(req.CatalogItemId, out var type))
                return UnprocessableEntity($"{prefix}.catalogItemId", ErrorCode.CatalogItemNotFound,
                    $"Catalog item '{req.CatalogItemId}' not found.");

            if (req.Kind == StocktakeItemKind.Standard)
            {
                if (type != CatalogItemType.Standard)
                    return UnprocessableEntity($"{prefix}.catalogItemId", ErrorCode.ValidationError,
                        "Standard lines require a standard catalog item.");

                if (req.CountedQuantity < 0)
                    return UnprocessableEntity($"{prefix}.countedQuantity", ErrorCode.OutOfRange,
                        "Counted quantity cannot be negative.");

                if (!string.IsNullOrWhiteSpace(req.InventoryNumber))
                    return UnprocessableEntity($"{prefix}.inventoryNumber", ErrorCode.ValidationError,
                        "Standard lines must not carry an inventory number.");
            }
            else
            {
                if (type != CatalogItemType.Unit)
                    return UnprocessableEntity($"{prefix}.catalogItemId", ErrorCode.ValidationError,
                        "Unit lines require a unit catalog item.");

                if (string.IsNullOrWhiteSpace(req.InventoryNumber))
                    return UnprocessableEntity($"{prefix}.inventoryNumber", ErrorCode.Required,
                        "Inventory number is required for unit lines.");

                if (req.CountedQuantity is < 0 or > 1)
                    return UnprocessableEntity($"{prefix}.countedQuantity", ErrorCode.OutOfRange,
                        "Unit lines can only be counted as 0 or 1.");
            }
        }

        var standardDuplicates = items
            .Where(x => x.Kind == StocktakeItemKind.Standard)
            .GroupBy(x => x.CatalogItemId)
            .Any(g => g.Count() > 1);
        if (standardDuplicates)
            return UnprocessableEntity("root", ErrorCode.ValidationError,
                "Duplicate standard item in request.");

        var unitDuplicates = items
            .Where(x => x.Kind == StocktakeItemKind.Unit)
            .GroupBy(x => (x.CatalogItemId, x.InventoryNumber?.Trim()))
            .Any(g => g.Count() > 1);
        if (unitDuplicates)
            return UnprocessableEntity("root", ErrorCode.ValidationError,
                "Duplicate inventory number in request.");

        return await ValidateUnitsAgainstDocumentAsync(stocktake, nodeId, items, ct);
    }

    /// <summary>Cross-cell checks: a serial belongs to this warehouse and is claimed found only once.</summary>
    private async Task<IActionResult?> ValidateUnitsAgainstDocumentAsync(
        Stocktake stocktake, Guid nodeId, IReadOnlyList<StocktakeItemRequest> items, CancellationToken ct)
    {
        var claimed = items
            .Where(i => i.Kind == StocktakeItemKind.Unit && i.CountedQuantity > 0)
            .Select(i => (i.CatalogItemId, Number: i.InventoryNumber!.Trim()))
            .ToList();

        if (claimed.Count == 0) return null;

        var numbers = claimed.Select(c => c.Number).ToList();
        var units = await db.InventoryItems.OfType<UnitInventoryItem>()
            .Where(u => numbers.Contains(u.InventoryNumber))
            .Select(u => new
            {
                u.Id,
                u.InventoryNumber,
                u.CatalogItemId,
                u.StoragePlaceNodeId,
                WarehouseId = u.StoragePlaceNodeId == null
                    ? (Guid?)null
                    : u.StoragePlaceNode!.RootStoragePlace.WarehouseId,
            })
            .ToListAsync(ct);

        // Same serial in two running counts: whichever finishes last decides where it lands, and the
        // other document keeps a phantom shortage. Surpluses count too — both would create the unit.
        var claimedInOtherCounts = await db.StocktakeItems
            .Where(i => i.Kind == StocktakeItemKind.Unit
                        && i.CountedQuantity > 0
                        && i.InventoryNumber != null
                        && numbers.Contains(i.InventoryNumber)
                        && i.StocktakeNode.StocktakeId != stocktake.Id
                        && i.StocktakeNode.Stocktake.Status == StocktakeStatus.InProgress)
            .Select(i => new
            {
                i.CatalogItemId,
                i.InventoryNumber,
                StocktakeId = i.StocktakeNode.StocktakeId,
                i.StocktakeNode.Stocktake.Number,
            })
            .ToListAsync(ct);

        foreach (var (catalogItemId, number) in claimed)
        {
            var unit = units.FirstOrDefault(u => u.CatalogItemId == catalogItemId && u.InventoryNumber == number);

            // Checked before the parallel-count clash: a foreign warehouse is the more permanent reason
            if (unit?.WarehouseId is { } warehouseId && warehouseId != stocktake.WarehouseId)
                return UnprocessableEntity("root", ErrorCode.StocktakeUnitItemInAnotherWarehouse,
                    $"Inventory number '{number}' belongs to another warehouse.",
                    new Dictionary<string, object> { ["inventoryNumber"] = number });

            var other = claimedInOtherCounts
                .FirstOrDefault(i => i.CatalogItemId == catalogItemId && i.InventoryNumber == number);

            if (other is not null)
                return UnprocessableEntity("root", ErrorCode.StocktakeUnitCountedTwice,
                    $"Inventory number '{number}' is already counted in stocktake #{other.Number}.",
                    new Dictionary<string, object>
                    {
                        ["inventoryNumber"] = number,
                        ["stocktakeId"]     = other.StocktakeId,
                        ["stocktakeNumber"] = other.Number,
                    });

            if (unit is null) continue;

            // The same serial claimed found in two cells would make the finish order ambiguous
            var claimedElsewhere = stocktake.Nodes
                .Where(n => n.StoragePlaceNodeId != nodeId)
                .SelectMany(n => n.Items)
                .Any(i => i.Kind == StocktakeItemKind.Unit
                          && i.CountedQuantity > 0
                          && i.CatalogItemId == catalogItemId
                          && i.InventoryNumber == number);

            if (claimedElsewhere)
                return UnprocessableEntity("root", ErrorCode.StocktakeUnitCountedTwice,
                    $"Inventory number '{number}' is already counted in another cell of this stocktake.",
                    new Dictionary<string, object> { ["inventoryNumber"] = number });
        }

        return null;
    }

    private async Task<Guid?> ResolveUnitIdAsync(Guid catalogItemId, string inventoryNumber, CancellationToken ct) =>
        await db.InventoryItems.OfType<UnitInventoryItem>()
            .Where(u => u.CatalogItemId == catalogItemId && u.InventoryNumber == inventoryNumber)
            .Select(u => (Guid?)u.Id)
            .FirstOrDefaultAsync(ct);

    // ── Status transitions ────────────────────────────────────────────────────

    /// <summary>Put a scheduled document on the calendar. Draft → Planned.</summary>
    /// <remarks>
    /// Draft status only. Errors: 404 <c>stocktakeNotFound</c>; 422
    /// <c>stocktakeInvalidStatusTransition</c> from any other status; 422 <c>validationError</c> on
    /// <c>plannedDate</c> when the document is not <c>Scheduled</c> or has no planned date; 422
    /// <c>stocktakeHasNoNodes</c> when the scope is empty; 403 <c>permissionDenied</c> /
    /// <c>stocktakeNotAssignedToWarehouse</c> (edit access).
    /// </remarks>
    [HttpPost("{id:guid}/schedule")]
    [Authorize]
    [ProducesResponseType<StocktakeDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Schedule(Guid id, CancellationToken ct = default)
    {
        var (stocktake, error) = await LoadStocktakeWithEditAccessAsync(id, ct, includeItems: true);
        if (error is not null) return error;

        if (stocktake!.Status != StocktakeStatus.Draft)
            return UnprocessableEntity("root", ErrorCode.StocktakeInvalidStatusTransition,
                "Only a Draft stocktake can be scheduled.");

        if (stocktake.Type != StocktakeType.Scheduled || stocktake.PlannedDate is null)
            return UnprocessableEntity("plannedDate", ErrorCode.ValidationError,
                "Only a scheduled stocktake with a planned date can be scheduled.");

        if (stocktake.Nodes.Count == 0)
            return UnprocessableEntity("root", ErrorCode.StocktakeHasNoNodes,
                "Select at least one storage node before scheduling.");

        var before = await BuildDtoAsync(stocktake, ct);

        stocktake.Status = StocktakeStatus.Planned;
        await db.SaveChangesAsync(ct);

        var after = await BuildDtoAsync(stocktake, ct);
        await changeLog.CompareAndSaveToChangelog(before, after, StocktakeActions.Scheduled);

        return Ok(after);
    }

    /// <summary>Return a scheduled document to work. Planned → Draft.</summary>
    /// <remarks>
    /// Planned status only. Errors: 404 <c>stocktakeNotFound</c>; 422
    /// <c>stocktakeInvalidStatusTransition</c> from any other status; 403 <c>permissionDenied</c> /
    /// <c>stocktakeNotAssignedToWarehouse</c> (edit access).
    /// </remarks>
    [HttpPost("{id:guid}/to-draft")]
    [Authorize]
    [ProducesResponseType<StocktakeDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> ToDraft(Guid id, CancellationToken ct = default)
    {
        var (stocktake, error) = await LoadStocktakeWithEditAccessAsync(id, ct, includeItems: true);
        if (error is not null) return error;

        if (stocktake!.Status != StocktakeStatus.Planned)
            return UnprocessableEntity("root", ErrorCode.StocktakeInvalidStatusTransition,
                "Only a Planned stocktake can be moved to Draft.");

        var before = await BuildDtoAsync(stocktake, ct);

        stocktake.Status = StocktakeStatus.Draft;
        await db.SaveChangesAsync(ct);

        var after = await BuildDtoAsync(stocktake, ct);
        await changeLog.CompareAndSaveToChangelog(before, after, StocktakeActions.MovedToDraft);

        return Ok(after);
    }

    /// <summary>Start counting. Draft or Planned → InProgress.</summary>
    /// <remarks>
    /// Draft or Planned only. Errors: 404 <c>stocktakeNotFound</c>; 422
    /// <c>stocktakeInvalidStatusTransition</c> from any other status; 422 <c>stocktakeHasNoNodes</c> when the
    /// scope is empty; 422 <c>stocktakeNodeAlreadyInProgress</c> when a cell in scope is already being counted
    /// in another InProgress stocktake, <c>args: { nodeId }</c>; 403 <c>permissionDenied</c> /
    /// <c>stocktakeNotAssignedToWarehouse</c> (edit access).
    /// </remarks>
    [HttpPost("{id:guid}/start")]
    [Authorize]
    [ProducesResponseType<StocktakeDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Start(Guid id, CancellationToken ct = default)
    {
        var (stocktake, error) = await LoadStocktakeWithEditAccessAsync(id, ct, includeItems: true);
        if (error is not null) return error;

        if (stocktake!.Status is not (StocktakeStatus.Draft or StocktakeStatus.Planned))
            return UnprocessableEntity("root", ErrorCode.StocktakeInvalidStatusTransition,
                "Only a Draft or Planned stocktake can be started.");

        if (stocktake.Nodes.Count == 0)
            return UnprocessableEntity("root", ErrorCode.StocktakeHasNoNodes,
                "Select at least one storage node before starting.");

        var conflict = await FindNodeCountedElsewhereAsync(
            id, stocktake.Nodes.Select(n => n.StoragePlaceNodeId).ToList(), ct);
        if (conflict is not null) return conflict;

        var before = await BuildDtoAsync(stocktake, ct);

        stocktake.Status    = StocktakeStatus.InProgress;
        stocktake.StartedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        var after = await BuildDtoAsync(stocktake, ct);
        await changeLog.CompareAndSaveToChangelog(before, after, StocktakeActions.Started);

        return Ok(after);
    }

    /// <summary>Return to scope editing. InProgress → Draft. Counted lines are kept.</summary>
    /// <remarks>
    /// InProgress status only. Errors: 404 <c>stocktakeNotFound</c>; 422
    /// <c>stocktakeInvalidStatusTransition</c> from any other status; 403 <c>permissionDenied</c> /
    /// <c>stocktakeNotAssignedToWarehouse</c> (edit access).
    /// </remarks>
    [HttpPost("{id:guid}/revert")]
    [Authorize]
    [ProducesResponseType<StocktakeDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Revert(Guid id, CancellationToken ct = default)
    {
        var (stocktake, error) = await LoadStocktakeWithEditAccessAsync(id, ct, includeItems: true);
        if (error is not null) return error;

        if (stocktake!.Status != StocktakeStatus.InProgress)
            return UnprocessableEntity("root", ErrorCode.StocktakeInvalidStatusTransition,
                "Only a stocktake in progress can be reverted to Draft.");

        var before = await BuildDtoAsync(stocktake, ct);

        // StartedAt is left in place: it records when counting first began, and Start overwrites it
        stocktake.Status = StocktakeStatus.Draft;
        await db.SaveChangesAsync(ct);

        var after = await BuildDtoAsync(stocktake, ct);
        await changeLog.CompareAndSaveToChangelog(before, after, StocktakeActions.Reverted);

        return Ok(after);
    }

    /// <summary>Cancel the stocktake without touching stock.</summary>
    /// <remarks>
    /// Errors: 404 <c>stocktakeNotFound</c>; 422 <c>stocktakeInvalidStatusTransition</c> from a terminal
    /// status (Finished or Canceled); 403 <c>permissionDenied</c> / <c>stocktakeNotAssignedToWarehouse</c>
    /// (edit access).
    /// </remarks>
    [HttpPost("{id:guid}/cancel")]
    [Authorize]
    [ProducesResponseType<StocktakeDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken ct = default)
    {
        var (stocktake, error) = await LoadStocktakeWithEditAccessAsync(id, ct, includeItems: true);
        if (error is not null) return error;

        if (stocktake!.Status is StocktakeStatus.Finished or StocktakeStatus.Canceled)
            return UnprocessableEntity("root", ErrorCode.StocktakeInvalidStatusTransition,
                $"Cannot cancel a stocktake in '{stocktake.Status}' status.");

        var before = await BuildDtoAsync(stocktake, ct);
        stocktake.Status = StocktakeStatus.Canceled;
        await db.SaveChangesAsync(ct);

        var after = await BuildDtoAsync(stocktake, ct);
        await changeLog.CompareAndSaveToChangelog(before, after, StocktakeActions.Canceled);

        return Ok(after);
    }

    // ── GET differences ───────────────────────────────────────────────────────

    /// <summary>
    /// What finishing would do, computed against live stock without mutating anything. The finish
    /// endpoint applies the very same plan.
    /// </summary>
    /// <remarks>
    /// Read-only, so blocking conditions come back inside the plan rather than as errors. Errors: 404
    /// <c>stocktakeNotFound</c>; 403 <c>permissionDenied</c> / <c>stocktakeNotAssignedToWarehouse</c>
    /// (view access); 401 <c>tokenInvalid</c>.
    /// </remarks>
    [HttpGet("{id:guid}/differences")]
    [Authorize]
    [ProducesResponseType<StocktakeDifferencesDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDifferences(Guid id, CancellationToken ct = default)
    {
        var (stocktake, error) = await LoadStocktakeWithViewAccessAsync(id, ct, includeItems: true);
        if (error is not null) return error;

        var plan = await diffCalculator.BuildPlanAsync(stocktake!, ct);
        return Ok(diffCalculator.ToDto(plan));
    }

    // ── POST finish ───────────────────────────────────────────────────────────

    /// <summary>
    /// Apply the count: bring live stock in line with what was counted. InProgress → Finished.
    /// Stock present in a counted cell but absent from the document is treated as counted zero.
    /// </summary>
    /// <remarks>
    /// InProgress status only; the whole plan is applied in one transaction, so any failure leaves stock
    /// untouched. Errors:
    /// <list type="bullet">
    ///   <item>404 <c>stocktakeNotFound</c></item>
    ///   <item>422 <c>stocktakeInvalidStatusTransition</c> — the document is not InProgress</item>
    ///   <item>422 <c>stocktakeHasNoNodes</c> — the scope is empty</item>
    ///   <item>422 <c>stocktakeUnitItemDetached</c> — a found serial is held by an active assembly
    ///     fulfillment; reported by the plan before anything is applied</item>
    ///   <item>422 <c>stocktakeUnitItemInAnotherWarehouse</c> — a counted serial is booked in another
    ///     warehouse; also reported by the plan</item>
    ///   <item>422 <c>insufficientInventory</c> — a shortage line asks for more than the cell holds;
    ///     <c>args: { itemName, requested, available, missing, path }</c></item>
    ///   <item>422 <c>stocktakeConcurrentModification</c> — a serial left its expected node while the finish
    ///     was running; the transaction rolled back</item>
    ///   <item>409 <c>inventoryWriteConflict</c> — concurrent stock writes outlasted the retry budget;
    ///     nothing was written and the request can be repeated</item>
    ///   <item>422 <c>unitInventoryItemNotFound</c> — a unit item disappeared mid-flight</item>
    ///   <item>422 <c>storagePlaceNodeNotFound</c> — a cell in scope no longer exists</item>
    ///   <item>422 <c>unitInventoryItemNumberDuplicate</c> — a surplus serial lost the race against the unique
    ///     index (field <c>inventoryNumber</c>)</item>
    ///   <item>403 <c>permissionDenied</c> / <c>stocktakeNotAssignedToWarehouse</c> (edit access)</item>
    /// </list>
    /// </remarks>
    [HttpPost("{id:guid}/finish")]
    [Authorize]
    [ProducesResponseType<StocktakeDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Finish(Guid id, CancellationToken ct = default)
    {
        var (stocktake, error) = await LoadStocktakeWithEditAccessAsync(id, ct, includeItems: true);
        if (error is not null) return error;

        if (stocktake!.Status != StocktakeStatus.InProgress)
            return UnprocessableEntity("root", ErrorCode.StocktakeInvalidStatusTransition,
                "Stocktake must be in progress to finish.");

        if (stocktake.Nodes.Count == 0)
            return UnprocessableEntity("root", ErrorCode.StocktakeHasNoNodes, "Stocktake has no nodes.");

        var before = await BuildDtoAsync(stocktake, ct);

        IActionResult? blocked = null;

        try
        {
            await db.Database.ExecuteInTransactionAsync("stocktakes.finish", async () =>
            {
                // Reload inside the transaction so the status check sees the committed state
                var fresh = await BaseQuery(includeItems: true).FirstAsync(s => s.Id == id, ct);
                if (fresh.Status != StocktakeStatus.InProgress)
                    return; // finished concurrently

                var plan = await diffCalculator.BuildPlanAsync(fresh, ct);
                if (plan.Problems.Count > 0)
                {
                    var problem = plan.Problems[0];
                    blocked = UnprocessableEntity("root", problem.Code, problem.Message);
                    return;
                }

                await ApplyPlanAsync(plan, fresh, ct);

                fresh.Status     = StocktakeStatus.Finished;
                fresh.FinishedAt = DateTime.UtcNow;
                await db.SaveChangesAsync(ct);
            }, ct);
        }
        catch (InventoryWriteConflictException)
        {
            return Conflict(ErrorCode.InventoryWriteConflict,
                "Stock for this item was changed concurrently; nothing was written.");
        }
        catch (InsufficientInventoryException ex)
        {
            return UnprocessableEntity("root", ErrorCode.InsufficientInventory,
                $"Insufficient inventory at node '{ex.NodeId}': requested {ex.Requested}, available {ex.Available}.",
                ex.ToArgs());
        }
        catch (InventoryItemNodeMismatchException)
        {
            return UnprocessableEntity("root", ErrorCode.StocktakeConcurrentModification,
                "Stock changed while the stocktake was being finished. Refresh and try again.");
        }
        catch (UnitInventoryItemNotFoundException)
        {
            return UnprocessableEntity("root", ErrorCode.UnitInventoryItemNotFound,
                "One or more unit items were not found.");
        }
        catch (StoragePlaceNodeNotFoundException)
        {
            return UnprocessableEntity("root", ErrorCode.StoragePlaceNodeNotFound, "Storage node not found.");
        }
        catch (Infrastructure.ValidationException ex)
        {
            return UnprocessableEntity(ex);
        }

        if (blocked is not null) return blocked;

        var after = await ReloadDtoAsync(id, ct);
        await changeLog.CompareAndSaveToChangelog(before, after, StocktakeActions.Finished);

        return Ok(after);
    }

    /// <summary>
    /// Applies the plan in a fixed order: relocations first so a serial moved between two counted cells
    /// is not detached by the cell that lost it, then detaches, then arrivals, then standard counts.
    /// </summary>
    private async Task ApplyPlanAsync(StocktakePlan plan, Stocktake fresh, CancellationToken ct)
    {
        var itemById = fresh.Nodes.SelectMany(n => n.Items).ToDictionary(i => i.Id);
        var scopeByNodeId = fresh.Nodes.ToDictionary(n => n.StoragePlaceNodeId);

        foreach (var line in Ordered(plan.Lines))
        {
            switch (line.Resolution)
            {
                case StocktakeDifferenceResolution.Relocation:
                    await inventory.MoveUnitItemAsync(
                        line.UnitInventoryItemId!.Value, line.StoragePlaceNodeId,
                        action: InventoryActions.StocktakeRelocation, ct: ct);
                    break;

                case StocktakeDifferenceResolution.DetachUnit:
                    await inventory.DetachUnitItemAsync(
                        line.UnitInventoryItemId!.Value, line.StoragePlaceNodeId,
                        action: InventoryActions.StocktakeShortage, ct: ct);
                    break;

                case StocktakeDifferenceResolution.ReattachUnit:
                    await inventory.ReattachUnitItemAsync(
                        line.UnitInventoryItemId!.Value, line.StoragePlaceNodeId,
                        action: InventoryActions.StocktakeSurplus, ct: ct);
                    break;

                case StocktakeDifferenceResolution.CreateUnit:
                    try
                    {
                        await inventory.PlaceUnitItemToNodeAsync(
                            line.StoragePlaceNodeId, line.CatalogItemId, line.InventoryNumber!,
                            action: InventoryActions.StocktakeSurplus, ct: ct);
                    }
                    catch (DbUpdateException)
                    {
                        // Race: the soft check passed but the unique index on the number fired
                        throw new Infrastructure.ValidationException("inventoryNumber",
                            ErrorCode.UnitInventoryItemNumberDuplicate,
                            $"Инвентарный номер «{line.InventoryNumber}» уже используется для этого товара.");
                    }
                    break;

                case StocktakeDifferenceResolution.Surplus:
                    await inventory.AddStandardItemsToNodeAsync(
                        line.StoragePlaceNodeId, line.CatalogItemId, line.Delta,
                        action: InventoryActions.StocktakeSurplus, ct: ct);
                    break;

                case StocktakeDifferenceResolution.Shortage:
                    await inventory.RemoveStandardItemsFromNodeAsync(
                        line.StoragePlaceNodeId, line.CatalogItemId, -line.Delta,
                        action: InventoryActions.StocktakeShortage, ct: ct);
                    break;
            }

            if (line.StocktakeItemId is { } itemId)
            {
                if (itemById.TryGetValue(itemId, out var item))
                    item.AppliedDelta = line.Delta;
            }
            else if (line.Resolution != StocktakeDifferenceResolution.NoChange)
            {
                // Stock the document never mentioned still got written off — persist it as a line so
                // the finished document shows the whole correction, not just the movement journal
                MaterializeLine(line, scopeByNodeId);
            }
        }
    }

    private void MaterializeLine(StocktakePlanLine line, Dictionary<Guid, StocktakeNode> scopeByNodeId)
    {
        if (!scopeByNodeId.TryGetValue(line.StoragePlaceNodeId, out var scopeNode)) return;

        db.StocktakeItems.Add(new StocktakeItem
        {
            Id                  = Guid.NewGuid(),
            StocktakeNodeId     = scopeNode.Id,
            Kind                = line.Kind,
            CatalogItemId       = line.CatalogItemId,
            CountedQuantity     = line.Counted,
            InventoryNumber     = line.InventoryNumber,
            UnitInventoryItemId = line.UnitInventoryItemId,
            Notes               = "Не указано в документе — обнулено при проведении",
            AppliedDelta        = line.Delta,
        });
    }

    private static IEnumerable<StocktakePlanLine> Ordered(IReadOnlyList<StocktakePlanLine> lines)
    {
        static int Rank(StocktakeDifferenceResolution r) => r switch
        {
            StocktakeDifferenceResolution.Relocation => 0,
            StocktakeDifferenceResolution.DetachUnit => 1,
            _ => 2,
        };

        return lines.OrderBy(l => Rank(l.Resolution));
    }
}
