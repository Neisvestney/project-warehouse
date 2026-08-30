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
using ProjectWarehouse.Server.Infrastructure.Realtime;
using ProjectWarehouse.Server.Integrations.Abstractions;
using ProjectWarehouse.Server.Models;
using ProjectWarehouse.Server.Models.Orders;
using ProjectWarehouse.Server.Services;

namespace ProjectWarehouse.Server.Controllers;

[Route("api/orders")]
// Orders have no changelog service, so staleness events come from the filter instead of one.
[PublishesEntityChanged(AppEntityType.Order)]
public class OrdersController(
    ApplicationDbContext db,
    IMapper mapper,
    IOrderService orders,
    IMarketplaceLabelService labels,
    EntityAccessRegistry access,
    AccessScope scope,
    IRealtimeNotifier realtime,
    ICatalogService catalog) : AppControllerBase
{
    private EntityAccessRule<Order> Rule => access.For<Order>();

    /// <summary>
    /// Browsing the order list needs a view permission of its own. The access rule also admits
    /// <c>assemble_assigned</c> — an assembler must be able to read the orders they work on — but that
    /// permission alone has never opened the order list, and widening it here is not part of this change.
    /// </summary>
    private bool CanBrowseOrders =>
        AccessScope.Has(User, Permissions.Orders.View) || AccessScope.Has(User, Permissions.Orders.ViewAssigned);

    // ── Base query helpers ────────────────────────────────────────────────────

    private IQueryable<Order> BaseQuery() =>
        db.Orders
            .Include(o => o.Warehouse)
            .Include(o => o.CreatedBy)
            // details are mapped in memory, so the marketplace block silently vanishes without this
            .Include(o => o.MarketplaceOrder).ThenInclude(m => m!.MarketplaceAccount);

    private IQueryable<Order> DetailsQuery() =>
        BaseQuery()
            .Include(o => o.Boxes).ThenInclude(b => b.Components).ThenInclude(c => c.CatalogItem).ThenInclude(ci => ci.Group)
            .Include(o => o.AssemblyTasks).ThenInclude(t => t.AssignedTo)
            .Include(o => o.AssemblyTasks).ThenInclude(t => t.Boxes).ThenInclude(tb => tb.OrderBox)
            .Include(o => o.AssemblyTasks).ThenInclude(t => t.Boxes)
                .ThenInclude(tb => tb.Components).ThenInclude(c => c.CatalogItem).ThenInclude(ci => ci.Group)
            .Include(o => o.AssemblyTasks).ThenInclude(t => t.Boxes)
                .ThenInclude(tb => tb.Components).ThenInclude(c => c.Fulfillments)
                .ThenInclude(f => f.BundleComponents).ThenInclude(bc => bc.CatalogItem).ThenInclude(ci => ci.Group)
            .Include(o => o.AssemblyTasks).ThenInclude(t => t.Boxes)
                .ThenInclude(tb => tb.Components).ThenInclude(c => c.Fulfillments)
                .ThenInclude(f => f.BundleComponents).ThenInclude(bc => bc.SourceNode).ThenInclude(n => n.RootStoragePlace)
            .Include(o => o.AssemblyTasks).ThenInclude(t => t.Boxes)
                .ThenInclude(tb => tb.Components).ThenInclude(c => c.Fulfillments)
                .ThenInclude(f => f.SourceNode).ThenInclude(n => n!.RootStoragePlace)
            .Include(o => o.AssemblyTasks).ThenInclude(t => t.Boxes)
                .ThenInclude(tb => tb.Components).ThenInclude(c => c.Fulfillments)
                .ThenInclude(f => f.ResolvedCatalogItem).ThenInclude(ci => ci!.Group)
            .Include(o => o.AssemblyTasks).ThenInclude(t => t.Boxes)
                .ThenInclude(tb => tb.Components).ThenInclude(c => c.Fulfillments)
                .ThenInclude(f => f.CreatedBy)
            .Include(o => o.MarketplaceItems)
                .ThenInclude(i => i.MarketplaceCard).ThenInclude(c => c!.CatalogItem)
            .AsSplitQuery();

    private async Task<Dictionary<Guid, StoragePlaceNode>> LoadWarehouseNodesAsync(
        IReadOnlyCollection<Guid> warehouseIds, CancellationToken ct) =>
        await db.StoragePlacesNodes
            .Where(n => warehouseIds.Contains(n.RootStoragePlace.WarehouseId))
            .Include(n => n.RootStoragePlace)
            .ToDictionaryAsync(n => n.Id, ct);

    // ── Access helpers ────────────────────────────────────────────────────────

    private async Task<(Order? order, IActionResult? error)> LoadOrderWithEditAccessAsync(
        Guid id, CancellationToken ct, bool fullDetails = false)
    {
        if (AccessError(await Rule.PrecheckAsync(User, AccessLevel.Edit, ct)) is { } prelude)
            return (null, prelude);

        var query  = fullDetails ? DetailsQuery() : BaseQuery();
        var order  = await query.FirstOrDefaultAsync(o => o.Id == id, ct);
        if (order is null)
            return (null, NotFound(ErrorCode.OrderNotFound, "Order not found."));

        if (AccessError(await Rule.CheckAsync(User, AccessLevel.Edit, order, ct)) is { } denied)
            return (null, denied);

        return (order, null);
    }

    /// <summary>
    /// Assembly is warehouse-bound for everyone, including holders of the unscoped <c>orders.edit</c> —
    /// physically picking stock requires being assigned to the warehouse it sits in.
    /// </summary>
    private async Task<(Order? order, IActionResult? error)> LoadOrderWithAssembleAccessAsync(
        Guid id, CancellationToken ct, bool fullDetails = false)
    {
        var canAssemble = AccessScope.Has(User, Permissions.Orders.AssembleAssigned);
        var canEdit     = AccessScope.Has(User, Permissions.Orders.Edit)
                       || AccessScope.Has(User, Permissions.Orders.EditAssigned);

        if (!canAssemble && !canEdit)
            return (null, Forbidden());

        var query = fullDetails ? DetailsQuery() : BaseQuery();
        var order = await query.FirstOrDefaultAsync(o => o.Id == id, ct);
        if (order is null)
            return (null, NotFound(ErrorCode.OrderNotFound, "Order not found."));

        var assignedIds = await scope.GetAssignedWarehouseIdsAsync(User, ct);
        if (assignedIds is null)
            return (null, Unauthorized(ErrorCode.TokenInvalid, "Invalid token."));
        if (!assignedIds.Contains(order.WarehouseId))
            return (null, Forbidden(ErrorCode.OrderNotAssignedToWarehouse,
                "You are not assigned to the warehouse of this order."));

        return (order, null);
    }

    private async Task<Order?> LoadOrderDetailsAsync(Guid id, CancellationToken ct) =>
        await DetailsQuery().FirstOrDefaultAsync(o => o.Id == id, ct);

    // ── GET /api/orders ───────────────────────────────────────────────────────

    /// <summary>List orders (paginated, filtered, sorted).</summary>
    /// <remarks>
    /// Query params: <c>page</c> (default 1), <c>pageSize</c> (default 20, max 200), <c>searchString</c>,
    /// <c>warehouseId</c>, <c>type</c>, <c>status</c>, <c>marketplaceType</c>, <c>marketplaceAccountId</c>,
    /// <c>marketplaceStatus</c>, <c>sortBy</c> (default <c>Number</c>), <c>sortOrder</c> (default <c>Desc</c>).
    /// Any of the three marketplace filters also excludes orders without a <c>MarketplaceOrder</c>, so they
    /// never match Direct orders. <c>searchString</c> is the extended search — it also matches box labels and
    /// the catalog items and marketplace cards of the order contents, see <see cref="Order.MatchesExtendedSearch"/>.
    /// Requires <c>orders.view</c> or <c>orders.view_assigned</c>; <c>orders.assemble_assigned</c> alone does
    /// not open the list (403).
    /// </remarks>
    [HttpGet]
    [Authorize]
    [ProducesResponseType<Paginated<OrderSummaryDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(
        [FromQuery][Range(1, int.MaxValue)] int page = 1,
        [FromQuery][Range(1, 200)] int pageSize = 20,
        [FromQuery] string? searchString = null,
        [FromQuery] Guid? warehouseId = null,
        [FromQuery] OrderType? type = null,
        [FromQuery] OrderStatus? status = null,
        [FromQuery] MarketplaceType? marketplaceType = null,
        [FromQuery] Guid? marketplaceAccountId = null,
        [FromQuery] MarketplaceOrderStatus? marketplaceStatus = null,
        [FromQuery] OrderSortBy sortBy = OrderSortBy.Number,
        [FromQuery] SortOrder sortOrder = SortOrder.Desc,
        CancellationToken ct = default)
    {
        if (!CanBrowseOrders)
            return Forbidden();

        if (AccessError(await Rule.PrecheckAsync(User, AccessLevel.View, ct)) is { } error)
            return error;

        var accessible = await Rule.QueryAsync(User, AccessLevel.View, ct);

        var baseQuery = accessible
            .Include(o => o.Warehouse)
            .Include(o => o.CreatedBy)
            .Include(o => o.Boxes).ThenInclude(b => b.Components)
            .Where(o => warehouseId == null || o.WarehouseId == warehouseId)
            .Where(o => type == null || o.Type == type)
            .Where(o => status == null || o.Status == status)
            .Where(o => marketplaceType == null ||
                        (o.MarketplaceOrder != null && o.MarketplaceOrder.MarketplaceAccount.Type == marketplaceType))
            .Where(o => marketplaceAccountId == null ||
                        (o.MarketplaceOrder != null && o.MarketplaceOrder.MarketplaceAccountId == marketplaceAccountId))
            .Where(o => marketplaceStatus == null ||
                        (o.MarketplaceOrder != null && o.MarketplaceOrder.Status == marketplaceStatus))
            .WhereMatchesExtendedSearch((o, pattern) => o.MatchesExtendedSearch(pattern), searchString);

        var query = sortBy switch
        {
            OrderSortBy.Status           => baseQuery.Sort(o => o.Status, sortOrder).ThenBy(o => o.Id),
            OrderSortBy.CreatedAt        => baseQuery.Sort(o => o.CreatedAt, sortOrder).ThenBy(o => o.Id),
            OrderSortBy.PlannedShipmentAt => baseQuery.Sort(o => o.PlannedShipmentAt, sortOrder).ThenBy(o => o.Id),
            OrderSortBy.WarehouseName    => baseQuery.Sort(o => o.Warehouse.Name, sortOrder).ThenBy(o => o.Id),
            _                            => baseQuery.Sort(o => o.Number, sortOrder).ThenBy(o => o.Id),
        };

        var paginated = await query
            .ProjectTo<OrderSummaryDto>(mapper.ConfigurationProvider)
            .ToPaginatedAsync(page, pageSize, ct);

        return Ok(paginated);
    }

    // ── GET /api/orders/assembly ──────────────────────────────────────────────

    /// <summary>The current user's personal assembly worklist: full details of Assembly-status orders that have a task assigned to them.</summary>
    /// <remarks>
    /// Query params: <c>warehouseId</c>, <c>searchString</c> (both optional). Not paginated — returns a plain list.
    /// <c>searchString</c> is the extended search — see <see cref="Order.MatchesExtendedSearch"/>.
    /// Only orders in <c>Assembly</c> status with at least one <c>AssemblyTask</c> assigned to the caller are
    /// returned, and each order carries only that caller's own tasks; other assemblers' tasks are filtered out.
    /// Every task box component is annotated with <c>containsUnit</c>, computed by walking the Bundle/Variation
    /// tree — the client uses it to exclude such tasks from batch assembly.
    /// Requires view access to orders (<c>orders.view</c>, <c>orders.view_assigned</c> or
    /// <c>orders.assemble_assigned</c>).
    /// </remarks>
    [HttpGet("assembly")]
    [Authorize]
    [ProducesResponseType<List<OrderDetailsDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllAssembly(
        [FromQuery] Guid? warehouseId = null,
        [FromQuery] string? searchString = null,
        CancellationToken ct = default)
    {
        if (AccessError(await Rule.PrecheckAsync(User, AccessLevel.View, ct)) is { } error)
            return error;

        var userId = GetCurrentUserId();
        if (userId is null)
            return Unauthorized(ErrorCode.TokenInvalid, "Invalid token.");

        var accessible = await Rule.QueryAsync(User, AccessLevel.View, ct);

        var query = accessible
            .Include(o => o.Warehouse)
            .Include(o => o.MarketplaceOrder!.MarketplaceAccount)
            .Include(o => o.CreatedBy)
            .Include(o => o.Boxes).ThenInclude(b => b.Components).ThenInclude(c => c.CatalogItem).ThenInclude(ci => ci.Group)
            .Include(o => o.AssemblyTasks.Where(t => t.AssignedToId == userId))
                .ThenInclude(t => t.AssignedTo)
            .Include(o => o.AssemblyTasks.Where(t => t.AssignedToId == userId))
                .ThenInclude(t => t.Boxes).ThenInclude(tb => tb.OrderBox)
            .Include(o => o.AssemblyTasks.Where(t => t.AssignedToId == userId))
                .ThenInclude(t => t.Boxes).ThenInclude(tb => tb.Components).ThenInclude(c => c.CatalogItem).ThenInclude(ci => ci.Group)
            .Include(o => o.AssemblyTasks.Where(t => t.AssignedToId == userId))
                .ThenInclude(t => t.Boxes).ThenInclude(tb => tb.Components)
                .ThenInclude(c => c.Fulfillments).ThenInclude(f => f.BundleComponents).ThenInclude(bc => bc.CatalogItem).ThenInclude(ci => ci.Group)
            .Include(o => o.AssemblyTasks.Where(t => t.AssignedToId == userId))
                .ThenInclude(t => t.Boxes).ThenInclude(tb => tb.Components)
                .ThenInclude(c => c.Fulfillments).ThenInclude(f => f.BundleComponents).ThenInclude(bc => bc.SourceNode).ThenInclude(n => n.RootStoragePlace)
            .Include(o => o.AssemblyTasks.Where(t => t.AssignedToId == userId))
                .ThenInclude(t => t.Boxes).ThenInclude(tb => tb.Components)
                .ThenInclude(c => c.Fulfillments).ThenInclude(f => f.SourceNode).ThenInclude(n => n!.RootStoragePlace)
            .Include(o => o.AssemblyTasks.Where(t => t.AssignedToId == userId))
                .ThenInclude(t => t.Boxes).ThenInclude(tb => tb.Components)
                .ThenInclude(c => c.Fulfillments).ThenInclude(f => f.ResolvedCatalogItem).ThenInclude(ci => ci!.Group)
            .Include(o => o.AssemblyTasks.Where(t => t.AssignedToId == userId))
                .ThenInclude(t => t.Boxes).ThenInclude(tb => tb.Components)
                .ThenInclude(c => c.Fulfillments).ThenInclude(f => f.CreatedBy)
            .Where(o => o.Status == OrderStatus.Assembly)
            .Where(o => o.AssemblyTasks.Any(t => t.AssignedToId == userId))
            .AsSplitQuery();
        
        if (warehouseId is not null)
            query = query.Where(o => o.WarehouseId == warehouseId);

        query = query.WhereMatchesExtendedSearch((o, pattern) => o.MatchesExtendedSearch(pattern), searchString);

        var result = await query.ToListAsync(ct);
        var nodeById = await LoadWarehouseNodesAsync(
            result.Select(o => o.WarehouseId).Distinct().ToList(), ct);
        var dtos = mapper.Map<List<OrderDetailsDto>>(result, opts => opts.Items["nodeById"] = nodeById);

        var componentDtos = dtos
            .SelectMany(o => o.AssemblyTasks)
            .SelectMany(t => t.Boxes)
            .SelectMany(b => b.Components)
            .ToList();
        var containsUnitByCatalogItemId = await catalog.ComputeContainsUnitAsync(
            componentDtos.Select(c => c.CatalogItemId).Distinct().ToList(), ct);
        foreach (var componentDto in componentDtos)
            componentDto.ContainsUnit = containsUnitByCatalogItemId.GetValueOrDefault(componentDto.CatalogItemId);

        return Ok(dtos);
    }

    // ── GET /api/orders/{id} ──────────────────────────────────────────────────

    /// <summary>Get full order details — boxes, components, assembly tasks with their fulfillments, and marketplace data.</summary>
    /// <remarks>
    /// Returns 404 <c>orderNotFound</c> if the order does not exist.
    /// Requires <c>orders.view</c> or <c>orders.view_assigned</c>; <c>orders.assemble_assigned</c> alone does
    /// not open the order page (403).
    /// </remarks>
    [HttpGet("{id:guid}")]
    [Authorize]
    [ProducesResponseType<OrderDetailsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct = default)
    {
        if (!CanBrowseOrders)
            return Forbidden();

        if (AccessError(await Rule.PrecheckAsync(User, AccessLevel.View, ct)) is { } prelude)
            return prelude;

        var order = await DetailsQuery().FirstOrDefaultAsync(o => o.Id == id, ct);
        if (order is null)
            return NotFound(ErrorCode.OrderNotFound, "Order not found.");

        if (AccessError(await Rule.CheckAsync(User, AccessLevel.View, order, ct)) is { } denied)
            return denied;

        var nodeById = await LoadWarehouseNodesAsync([order.WarehouseId], ct);
        return Ok(mapper.Map<OrderDetailsDto>(order, opts => opts.Items["nodeById"] = nodeById));
    }

    // ── POST /api/orders/direct ───────────────────────────────────────────────

    /// <summary>Create a Direct (non-marketplace) order in Draft status.</summary>
    /// <remarks>
    /// Body: <c>CreateDirectOrderRequest</c> — warehouseId (required), notes, plannedShipmentAt.
    /// The order is created empty; boxes and components are added afterwards.
    /// Returns 422 <c>warehouseNotFound</c> for an unknown warehouse.
    /// Requires <c>orders.edit</c>, or <c>orders.edit_assigned</c> for the target warehouse.
    /// </remarks>
    [HttpPost("direct")]
    [Authorize]
    [ProducesResponseType<OrderDetailsDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> CreateDirect([FromBody] CreateDirectOrderRequest request, CancellationToken ct = default)
    {
        if (AccessError(await Rule.PrecheckAsync(User, AccessLevel.Edit, ct)) is { } error)
            return error;

        var warehouse = await db.Warehouses.FindAsync([request.WarehouseId], ct);
        if (warehouse is null)
            return UnprocessableEntity("warehouseId", ErrorCode.WarehouseNotFound, "Warehouse not found.");

        if (AccessError(await Rule.CheckWarehouseAsync(User, AccessLevel.Edit, request.WarehouseId, ct)) is { } denied)
            return denied;

        var order = await orders.CreateDirectOrderAsync(request, GetCurrentUserId(), ct);

        var full = await LoadOrderDetailsAsync(order.Id, ct);
        return CreatedAtAction(nameof(GetById), new { id = order.Id }, mapper.Map<OrderDetailsDto>(full));
    }

    // ── PUT /api/orders/{id} ──────────────────────────────────────────────────

    /// <summary>Update an order's notes and planned shipment date.</summary>
    /// <remarks>
    /// Body: <c>UpdateOrderRequest</c> — only <c>notes</c> and <c>plannedShipmentAt</c> are writable here;
    /// composition and status are changed through their own endpoints. Allowed in any status.
    /// Returns 404 <c>orderNotFound</c>. Requires <c>orders.edit</c> or <c>orders.edit_assigned</c>.
    /// </remarks>
    [HttpPut("{id:guid}")]
    [Authorize]
    [ProducesResponseType<OrderDetailsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateOrderRequest request, CancellationToken ct = default)
    {
        var (order, error) = await LoadOrderWithEditAccessAsync(id, ct);
        if (error is not null) return error;

        await orders.UpdateOrderAsync(order!, request, ct);

        var full = await LoadOrderDetailsAsync(id, ct);
        return Ok(mapper.Map<OrderDetailsDto>(full));
    }

    // ── DELETE /api/orders/{id} ───────────────────────────────────────────────

    /// <summary>Delete an order. Only allowed in Draft status.</summary>
    /// <remarks>
    /// Returns 422 <c>orderNotDraft</c> for any other status, 404 <c>orderNotFound</c> if it does not exist.
    /// Requires <c>orders.edit</c> or <c>orders.edit_assigned</c>.
    /// </remarks>
    [HttpDelete("{id:guid}")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct = default)
    {
        var (order, error) = await LoadOrderWithEditAccessAsync(id, ct);
        if (error is not null) return error;

        try
        {
            await orders.DeleteOrderAsync(order!, ct);
        }
        catch (ValidationException ex)
        {
            return UnprocessableEntity(ex);
        }

        return NoContent();
    }

    // ── PUT /api/orders/{id}/status ───────────────────────────────────────────

    /// <summary>Move the order to another status.</summary>
    /// <remarks>
    /// Body: <c>TransitionOrderStatusRequest</c> — <c>targetStatus</c>. Allowed transitions:
    /// <list type="bullet">
    ///   <item>Draft → Confirmed | Canceled</item>
    ///   <item>Confirmed → Draft | Assembly | Canceled</item>
    ///   <item>Assembly → Confirmed | Assembled | Canceled</item>
    ///   <item>Assembled → Shipped</item>
    ///   <item>Shipped → Assembled</item>
    /// </list>
    /// Anything else is 422 <c>orderInvalidStatusTransition</c>. Assembly → Confirmed is additionally refused
    /// when any assembly task is already <c>Done</c>; otherwise it deletes every assembly task of the order and
    /// restores the inventory of their fulfillments. Assembled is normally reached automatically when the last
    /// task turns Done with every component fully fulfilled (see the assembly-task status endpoint); Assembly →
    /// Assembled through this endpoint is the manual recovery path for when that condition became true only
    /// after the last task was already Done (e.g. a missing fulfillment was added afterwards) — it re-checks the
    /// same condition (every task Done and fully fulfilled) and is refused with 422
    /// <c>orderInvalidStatusTransition</c> otherwise. Shipped → Assembled is a pure status change — inventory was
    /// already deducted when fulfillments were added during assembly and is not touched by shipment or its
    /// rollback. Cancelling is refused with 422 <c>orderHasFulfillments</c>
    /// while any fulfillment still exists. Returns 404 <c>orderNotFound</c>, 409 <c>inventoryWriteConflict</c>
    /// when the inventory restored by Assembly → Confirmed loses to concurrent stock writes — nothing was
    /// written and the request can be repeated.
    /// Requires <c>orders.edit</c> or <c>orders.edit_assigned</c>.
    /// </remarks>
    [HttpPut("{id:guid}/status")]
    [Authorize]
    [ProducesResponseType<OrderDetailsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> TransitionStatus(
        Guid id, [FromBody] TransitionOrderStatusRequest request, CancellationToken ct = default)
    {
        var (order, error) = await LoadOrderWithEditAccessAsync(id, ct, fullDetails: true);
        if (error is not null) return error;

        try
        {
            await orders.TransitionOrderStatusAsync(order!, request.TargetStatus, ct);
        }
        catch (ValidationException ex)
        {
            return UnprocessableEntity(ex);
        }
        catch (InventoryWriteConflictException)
        {
            return Conflict(ErrorCode.InventoryWriteConflict,
                "Stock for this item was changed concurrently; nothing was written.");
        }

        var full = await LoadOrderDetailsAsync(id, ct);
        return Ok(mapper.Map<OrderDetailsDto>(full));
    }

    // ── POST /api/orders/{id}/self-assign ─────────────────────────────────────

    /// <summary>Take a Confirmed order for yourself: moves it to Assembly and creates one task with all of its boxes, assigned to the caller.</summary>
    /// <remarks>
    /// Requires <c>orders.self_assign</c> and an assignment to the order's warehouse — otherwise 403, with
    /// <c>orderNotAssignedToWarehouse</c> in the latter case. The warehouse check is skipped for holders of the
    /// unscoped <c>orders.view</c>, who see every order anyway. Returns 422 <c>orderNotConfirmed</c> if the order
    /// is in any other status, 404 <c>orderNotFound</c> if it does not exist.
    /// </remarks>
    [HttpPost("{id:guid}/self-assign")]
    [Authorize]
    [ProducesResponseType<OrderDetailsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> SelfAssign(Guid id, CancellationToken ct = default)
    {
        if (!User.HasClaim("permission", Permissions.Orders.SelfAssign))
            return Forbidden();

        var narrowing = await scope.GetWarehouseNarrowingAsync(User, Permissions.Orders.View, ct);
        if (AccessError(narrowing.Verdict) is { } tokenError)
            return tokenError;

        var order = await DetailsQuery().FirstOrDefaultAsync(o => o.Id == id, ct);
        if (order is null)
            return NotFound(ErrorCode.OrderNotFound, "Order not found.");

        if (narrowing.Ids is { } assignedIds && !assignedIds.Contains(order.WarehouseId))
            return Forbidden(ErrorCode.OrderNotAssignedToWarehouse,
                "You are not assigned to the warehouse of this order.");

        var userId = GetCurrentUserId();
        if (userId is null)
            return Unauthorized(ErrorCode.TokenInvalid, "Invalid token.");

        try
        {
            await orders.SelfAssignOrderAsync(order, userId.Value, ct);
        }
        catch (ValidationException ex)
        {
            return UnprocessableEntity(ex);
        }

        var full = await LoadOrderDetailsAsync(id, ct);
        return Ok(mapper.Map<OrderDetailsDto>(full));
    }

    // ── POST /api/orders/labels ───────────────────────────────────────────────

    /// <summary>Marketplace labels for the given orders, merged into one printable PDF.</summary>
    /// <remarks>
    /// Lives here rather than under integrations because it is invoked from the order list and is
    /// scoped by warehouse like every other order operation.
    /// Body: <c>orderIds</c> (deduplicated, at most <see cref="MaxLabelOrders"/>) and an optional
    /// <c>grouping</c>. Answers <c>application/pdf</c> — one merged document in the order the ids were sent.
    /// <para>All or nothing: if any requested label is missing the file is withheld entirely. A batch of 30
    /// quietly arriving with 28 labels means two unshipped boxes.</para>
    /// <list type="bullet">
    ///   <item>422 <c>required</c> — empty <c>orderIds</c></item>
    ///   <item>422 <c>outOfRange</c> (<c>args.max</c>) — more than <see cref="MaxLabelOrders"/> requested</item>
    ///   <item>403 <c>orderNotAssignedToWarehouse</c> — an order lies outside the caller's warehouses</item>
    ///   <item>422 <c>marketplaceOrderNotFromMarketplace</c> (<c>args.orderIds</c>) — an order has no posting</item>
    ///   <item>422 <c>marketplaceOrderNotAwaitingDeliver</c> (<c>args.postingNumbers</c>, <c>args.count</c>) —
    ///     a label is not stored yet and its posting is not awaiting shipment</item>
    ///   <item>409 <c>marketplaceLabelNotReady</c> (<c>args.postingNumbers</c>, <c>args.count</c>) — the
    ///     marketplace has not produced every label yet</item>
    ///   <item>502 <c>marketplaceApiError</c> (<c>args.marketplaceStatus</c>, <c>args.marketplaceResponse</c>)</item>
    /// </list>
    /// <c>count</c> travels beside <c>postingNumbers</c> because the client interpolates a scalar to pluralize;
    /// an array cannot. Per-posting labels are cached in <c>DataFile</c>, so a repeat call does not hit the
    /// marketplace; the merged document itself is not stored.
    /// Requires <c>orders.view</c>, or <c>orders.view_assigned</c> limited to the caller's warehouses.
    /// </remarks>
    [HttpPost("labels")]
    [Authorize]
    [Produces("application/pdf", "application/problem+json")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> GetLabels([FromBody] OrderLabelsRequest request, CancellationToken ct = default)
    {
        var orderIds = request.OrderIds.Distinct().ToList();
        if (orderIds.Count == 0)
            return UnprocessableEntity(nameof(request.OrderIds), ErrorCode.Required, "No orders were requested.");

        if (orderIds.Count > MaxLabelOrders)
            return UnprocessableEntity(nameof(request.OrderIds), ErrorCode.OutOfRange,
                $"At most {MaxLabelOrders} labels can be printed at once.",
                new Dictionary<string, object> { ["max"] = MaxLabelOrders });

        if (!CanBrowseOrders)
            return Forbidden();

        if (AccessError(await Rule.PrecheckAsync(User, AccessLevel.View, ct)) is { } error)
            return error;

        var narrowing = await scope.GetWarehouseNarrowingAsync(User, Permissions.Orders.View, ct);
        if (AccessError(narrowing.Verdict) is { } tokenError)
            return tokenError;

        if (narrowing.Ids is { } assignedIds)
        {
            var outside = await db.Orders
                .AnyAsync(o => orderIds.Contains(o.Id) && !assignedIds.Contains(o.WarehouseId), ct);

            if (outside)
                return Forbidden(ErrorCode.OrderNotAssignedToWarehouse,
                    "Some of the orders belong to a warehouse you are not assigned to.");
        }

        LabelBundle bundle;
        try
        {
            bundle = await labels.BuildAsync(orderIds, request.Grouping, GetCurrentUserId(), ct);
        }
        catch (ValidationException ex)
        {
            return UnprocessableEntity(ex);
        }
        catch (MarketplaceApiException ex)
        {
            return Problem(AppProblems.Root(StatusCodes.Status502BadGateway,
                ErrorCode.MarketplaceApiError, ex.Message, ex.Args));
        }

        if (bundle.NonMarketplaceOrderIds.Count > 0)
            return UnprocessableEntity(nameof(request.OrderIds), ErrorCode.MarketplaceOrderNotFromMarketplace,
                "Some of the orders did not come from a marketplace.",
                new Dictionary<string, object> { ["orderIds"] = bundle.NonMarketplaceOrderIds });

        if (bundle.NotAwaitingDeliverPostingNumbers.Count > 0)
            return UnprocessableEntity(nameof(request.OrderIds), ErrorCode.MarketplaceOrderNotAwaitingDeliver,
                "A label that is not stored yet can only be printed for a posting awaiting shipment.",
                new Dictionary<string, object>
                {
                    ["postingNumbers"] = bundle.NotAwaitingDeliverPostingNumbers,
                    ["count"] = bundle.NotAwaitingDeliverPostingNumbers.Count,
                });

        if (bundle.NotReadyPostingNumbers.Count > 0)
            // count travels separately: the client interpolates a scalar, an array does not pluralize
            return Problem(AppProblems.Root(StatusCodes.Status409Conflict, ErrorCode.MarketplaceLabelNotReady,
                "The marketplace has not produced all of the labels yet.",
                new Dictionary<string, object>
                {
                    ["postingNumbers"] = bundle.NotReadyPostingNumbers,
                    ["count"] = bundle.NotReadyPostingNumbers.Count,
                }));

        // a MemoryStream, because PdfDocument.Save needs a seekable target and Response.Body is not one
        return File(new MemoryStream(bundle.Pdf!), "application/pdf", "labels.pdf");
    }

    /// <summary>A print job bigger than this is a misclick, not a shift's work.</summary>
    private const int MaxLabelOrders = 200;

    // ── POST /api/orders/batch-self-assign ────────────────────────────────────

    /// <summary>Self-assign several orders in one request, with partial-success semantics.</summary>
    /// <remarks>
    /// Body: <c>BatchSelfAssignRequest</c> — <c>orderIds</c> (duplicates are collapsed). Each order is checked
    /// independently and always answers 200 with <c>BatchSelfAssignResponse</c>: successful ids in
    /// <c>assignedOrderIds</c>, the rest in <c>failedItems</c> as <c>{ orderId, orderNumber, error }</c> with the
    /// real error code (<c>orderNotFound</c>, <c>orderNotAssignedToWarehouse</c>, <c>orderNotConfirmed</c>, …).
    /// There is no transaction: already-assigned orders stay assigned when later ones fail.
    /// 403 is returned only for the request as a whole, when <c>orders.self_assign</c> is missing.
    /// Holders of the unscoped <c>orders.view</c> are not narrowed to their assigned warehouses.
    /// The route carries no id, so realtime change events are published explicitly for each assigned order.
    /// </remarks>
    [HttpPost("batch-self-assign")]
    [Authorize]
    [ProducesResponseType<BatchSelfAssignResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> BatchSelfAssign(
        [FromBody] BatchSelfAssignRequest request, CancellationToken ct = default)
    {
        if (!User.HasClaim("permission", Permissions.Orders.SelfAssign))
            return Forbidden();

        var narrowing = await scope.GetWarehouseNarrowingAsync(User, Permissions.Orders.View, ct);
        if (AccessError(narrowing.Verdict) is { } tokenError)
            return tokenError;

        var userId = GetCurrentUserId();
        if (userId is null)
            return Unauthorized(ErrorCode.TokenInvalid, "Invalid token.");

        var assignedOrderIds = new List<Guid>();
        var failedItems      = new List<BatchSelfAssignFailedItem>();

        void Fail(Guid orderId, ErrorCode code, string message, int? number = null) =>
            failedItems.Add(new BatchSelfAssignFailedItem
            {
                OrderId     = orderId,
                OrderNumber = number,
                Error       = AppProblems.MakeError(code, message),
            });

        foreach (var orderId in request.OrderIds.Distinct())
        {
            var order = await db.Orders.FirstOrDefaultAsync(o => o.Id == orderId, ct);
            if (order is null)
            {
                Fail(orderId, ErrorCode.OrderNotFound, "Order not found.");
                continue;
            }

            if (narrowing.Ids is { } assignedIds && !assignedIds.Contains(order.WarehouseId))
            {
                Fail(orderId, ErrorCode.OrderNotAssignedToWarehouse,
                    "You are not assigned to the warehouse of this order.", order.Number);
                continue;
            }

            try
            {
                await orders.SelfAssignOrderAsync(order, userId.Value, ct);
                assignedOrderIds.Add(orderId);
            }
            catch (ValidationException ex)
            {
                Fail(orderId, ex.ErrorCode, ex.Message, order.Number);
            }
        }

        // The route carries no id, so the filter cannot see which orders this touched.
        foreach (var orderId in assignedOrderIds)
            await realtime.PublishEntityChangedAsync(AppEntityType.Order, orderId, User, ct);

        return Ok(new BatchSelfAssignResponse
        {
            AssignedOrderIds = assignedOrderIds,
            FailedItems      = failedItems,
        });
    }

    // ── POST /api/orders/batch-transition-status ──────────────────────────────

    /// <summary>Transition several orders to the same target status in one request, with partial-success semantics.</summary>
    /// <remarks>
    /// Body: <c>BatchTransitionStatusRequest</c> — <c>orderIds</c> (duplicates are collapsed) and
    /// <c>targetStatus</c>, the same one for every order. Each order is transitioned independently through
    /// <see cref="IOrderService.TransitionOrderStatusAsync"/> — same allowed transitions and side effects as the
    /// single-order <c>PUT /{id}/status</c> — and the endpoint always answers 200 with
    /// <c>BatchTransitionStatusResponse</c>: successful ids in <c>transitionedOrderIds</c>, the rest in
    /// <c>failedItems</c> as <c>{ orderId, orderNumber, error }</c> with the real error code (<c>orderNotFound</c>,
    /// <c>orderInvalidStatusTransition</c>, …). There is no transaction: orders already transitioned stay
    /// transitioned when later ones fail.
    /// 403 is returned only for the request as a whole, when edit access is missing entirely. An order the
    /// caller cannot edit (outside their assigned warehouses) is reported as <c>orderNotFound</c> in
    /// <c>failedItems</c> rather than a distinct forbidden error, matching <see cref="LoadOrderWithEditAccessAsync"/>'s
    /// underlying access rule.
    /// Requires <c>orders.edit</c> or <c>orders.edit_assigned</c>.
    /// </remarks>
    [HttpPost("batch-transition-status")]
    [Authorize]
    [ProducesResponseType<BatchTransitionStatusResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> BatchTransitionStatus(
        [FromBody] BatchTransitionStatusRequest request, CancellationToken ct = default)
    {
        if (AccessError(await Rule.PrecheckAsync(User, AccessLevel.Edit, ct)) is { } error)
            return error;

        // ValidateOrderTransition reads order.AssemblyTasks (and its Fulfillments) to block Assembly → Confirmed
        // with a Done task and any → Canceled with existing fulfillments — without these includes the
        // collections come back empty and both checks silently no-op.
        var accessible = (await Rule.QueryAsync(User, AccessLevel.Edit, ct))
            .Include(o => o.AssemblyTasks).ThenInclude(t => t.Boxes).ThenInclude(b => b.Components)
                .ThenInclude(c => c.Fulfillments);

        var transitionedOrderIds = new List<Guid>();
        var failedItems          = new List<BatchTransitionStatusFailedItem>();

        void Fail(Guid orderId, ErrorCode code, string message, int? number = null) =>
            failedItems.Add(new BatchTransitionStatusFailedItem
            {
                OrderId     = orderId,
                OrderNumber = number,
                Error       = AppProblems.MakeError(code, message),
            });

        foreach (var orderId in request.OrderIds.Distinct())
        {
            var order = await accessible.FirstOrDefaultAsync(o => o.Id == orderId, ct);
            if (order is null)
            {
                Fail(orderId, ErrorCode.OrderNotFound, "Order not found.");
                continue;
            }

            try
            {
                await orders.TransitionOrderStatusAsync(order, request.TargetStatus, ct);
                transitionedOrderIds.Add(orderId);
            }
            catch (ValidationException ex)
            {
                Fail(orderId, ex.ErrorCode, ex.Message, order.Number);
            }
            catch (InventoryWriteConflictException)
            {
                Fail(orderId, ErrorCode.InventoryWriteConflict,
                    "Stock for this item was changed concurrently; nothing was written.", order.Number);
            }
        }

        // The route carries no id, so the filter cannot see which orders this touched.
        foreach (var orderId in transitionedOrderIds)
            await realtime.PublishEntityChangedAsync(AppEntityType.Order, orderId, User, ct);

        return Ok(new BatchTransitionStatusResponse
        {
            TransitionedOrderIds = transitionedOrderIds,
            FailedItems          = failedItems,
        });
    }

    // ── POST /api/orders/{id}/boxes ───────────────────────────────────────────

    /// <summary>Add an empty box to the order.</summary>
    /// <remarks>
    /// Body: <c>CreateOrderBoxRequest</c> — <c>label</c>.
    /// Allowed in Draft, Confirmed and Assembly; any other status is 422 <c>orderInvalidStatusTransition</c>.
    /// Permission depends on status: in Draft/Confirmed <c>orders.edit</c> / <c>orders.edit_assigned</c> or
    /// <c>orders.assemble_assigned</c> is enough, but in Assembly only <c>orders.assemble_assigned</c> is
    /// accepted — during assembly boxes are managed by the assembler, not the admin page.
    /// Callers without the unscoped <c>orders.edit</c> must be assigned to the order's warehouse
    /// (403 <c>orderNotAssignedToWarehouse</c>). Returns 404 <c>orderNotFound</c>.
    /// </remarks>
    [HttpPost("{id:guid}/boxes")]
    [Authorize]
    [ProducesResponseType<OrderBoxDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> AddBox(Guid id, [FromBody] CreateOrderBoxRequest request, CancellationToken ct = default)
    {
        // In Assembly status: assembler can add boxes. In Draft/Confirmed: admin/editor only.
        var order = await BaseQuery().FirstOrDefaultAsync(o => o.Id == id, ct);
        if (order is null)
            return NotFound(ErrorCode.OrderNotFound, "Order not found.");

        var canEdit     = User.HasClaim("permission", Permissions.Orders.Edit)
                       || User.HasClaim("permission", Permissions.Orders.EditAssigned);
        var canAssemble = User.HasClaim("permission", Permissions.Orders.AssembleAssigned);

        if (!canEdit && !canAssemble)
            return Forbidden();

        if (order.Status == OrderStatus.Assembly && !canAssemble)
            return Forbidden();

        if (order.Status is not (OrderStatus.Draft or OrderStatus.Confirmed or OrderStatus.Assembly))
            return UnprocessableEntity("root", ErrorCode.OrderInvalidStatusTransition,
                "Boxes can only be added in Draft, Confirmed, or Assembly status.");

        var assignedIds = canEdit
            ? null
            : await scope.GetAssignedWarehouseIdsAsync(User, ct);

        if (assignedIds is not null && !assignedIds.Contains(order.WarehouseId))
            return Forbidden(ErrorCode.OrderNotAssignedToWarehouse, "You are not assigned to this order's warehouse.");

        var box = await orders.AddBoxAsync(order, request, ct);
        return CreatedAtAction(nameof(GetById), new { id = order.Id }, mapper.Map<OrderBoxDto>(box));
    }

    // ── PUT /api/orders/{id}/boxes/{boxId} ────────────────────────────────────

    /// <summary>Rename a box.</summary>
    /// <remarks>
    /// Body: <c>UpdateOrderBoxRequest</c> — <c>label</c>; the box contents are not touched, and no status
    /// restriction applies (the label stays editable even during Assembly).
    /// Returns 404 <c>orderNotFound</c> or <c>orderBoxNotFound</c>.
    /// Requires <c>orders.edit</c> or <c>orders.edit_assigned</c>.
    /// </remarks>
    [HttpPut("{id:guid}/boxes/{boxId:guid}")]
    [Authorize]
    [ProducesResponseType<OrderBoxDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateBox(Guid id, Guid boxId, [FromBody] UpdateOrderBoxRequest request, CancellationToken ct = default)
    {
        var (order, error) = await LoadOrderWithEditAccessAsync(id, ct);
        if (error is not null) return error;

        var box = await db.OrderBoxes.FirstOrDefaultAsync(b => b.Id == boxId && b.OrderId == id, ct);
        if (box is null)
            return NotFound(ErrorCode.OrderBoxNotFound, "Box not found.");

        await orders.UpdateBoxAsync(box, request, ct);
        return Ok(mapper.Map<OrderBoxDto>(box));
    }

    // ── DELETE /api/orders/{id}/boxes/{boxId} ─────────────────────────────────

    /// <summary>Delete a box. Only an empty box can be deleted.</summary>
    /// <remarks>
    /// Returns 422 <c>validationError</c> if the box still has components, 404 <c>orderNotFound</c> or
    /// <c>orderBoxNotFound</c>.
    /// Requires <c>orders.edit</c> / <c>orders.edit_assigned</c> or <c>orders.assemble_assigned</c>; while the
    /// order is in Assembly only <c>orders.assemble_assigned</c> is accepted.
    /// </remarks>
    [HttpDelete("{id:guid}/boxes/{boxId:guid}")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> RemoveBox(Guid id, Guid boxId, CancellationToken ct = default)
    {
        var order = await BaseQuery().FirstOrDefaultAsync(o => o.Id == id, ct);
        if (order is null)
            return NotFound(ErrorCode.OrderNotFound, "Order not found.");

        var canEdit     = User.HasClaim("permission", Permissions.Orders.Edit)
                       || User.HasClaim("permission", Permissions.Orders.EditAssigned);
        var canAssemble = User.HasClaim("permission", Permissions.Orders.AssembleAssigned);

        if (!canEdit && !canAssemble)
            return Forbidden();

        if (order.Status == OrderStatus.Assembly && !canAssemble)
            return Forbidden();

        var box = await db.OrderBoxes.Include(b => b.Components)
            .FirstOrDefaultAsync(b => b.Id == boxId && b.OrderId == id, ct);
        if (box is null)
            return NotFound(ErrorCode.OrderBoxNotFound, "Box not found.");

        try
        {
            await orders.RemoveBoxAsync(box, ct);
        }
        catch (ValidationException ex)
        {
            return UnprocessableEntity(ex);
        }

        return NoContent();
    }

    // ── POST /api/orders/{id}/boxes/{boxId}/components ────────────────────────

    /// <summary>Add a catalog item to a box, or overwrite its quantity if the box already holds that item.</summary>
    /// <remarks>
    /// Body: <c>UpsertOrderBoxComponentRequest</c> — <c>catalogItemId</c>, <c>quantity</c>. Upsert: an existing
    /// component for the same catalog item has its quantity replaced rather than summed.
    /// Allowed only in Draft or Confirmed — otherwise 422 <c>orderInvalidStatusTransition</c>.
    /// Returns 422 <c>catalogItemNotFound</c>, 404 <c>orderNotFound</c> or <c>orderBoxNotFound</c>.
    /// Requires <c>orders.edit</c> or <c>orders.edit_assigned</c>.
    /// </remarks>
    [HttpPost("{id:guid}/boxes/{boxId:guid}/components")]
    [Authorize]
    [ProducesResponseType<OrderBoxComponentDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> AddComponent(
        Guid id, Guid boxId, [FromBody] UpsertOrderBoxComponentRequest request, CancellationToken ct = default)
    {
        var (order, error) = await LoadOrderWithEditAccessAsync(id, ct);
        if (error is not null) return error;

        if (order!.Status is not (OrderStatus.Draft or OrderStatus.Confirmed))
            return UnprocessableEntity("root", ErrorCode.OrderInvalidStatusTransition,
                "Components can only be added in Draft or Confirmed status.");

        var box = await db.OrderBoxes
            .Include(b => b.Components)
            .FirstOrDefaultAsync(b => b.Id == boxId && b.OrderId == id, ct);
        if (box is null)
            return NotFound(ErrorCode.OrderBoxNotFound, "Box not found.");

        var catalogItem = await db.CatalogItems.FindAsync([request.CatalogItemId], ct);
        if (catalogItem is null)
            return UnprocessableEntity("catalogItemId", ErrorCode.CatalogItemNotFound, "Catalog item not found.");

        var component = await orders.UpsertBoxComponentAsync(box, request.CatalogItemId, request.Quantity, ct);
        await db.Entry(component).Reference(c => c.CatalogItem).LoadAsync(ct);
        await db.Entry(component.CatalogItem).Reference(c => c.Group).LoadAsync(ct);

        return CreatedAtAction(nameof(GetById), new { id }, mapper.Map<OrderBoxComponentDto>(component));
    }

    // ── PUT /api/orders/{id}/boxes/{boxId}/components/{cid} ──────────────────

    /// <summary>Change a box component's catalog item and quantity.</summary>
    /// <remarks>
    /// Body: <c>UpsertOrderBoxComponentRequest</c>. Allowed only in Draft or Confirmed — otherwise 422
    /// <c>orderInvalidStatusTransition</c>. Returns 422 <c>catalogItemNotFound</c> when switching to an unknown
    /// item, 404 <c>orderNotFound</c>, <c>orderBoxNotFound</c> or <c>orderBoxComponentNotFound</c>.
    /// Requires <c>orders.edit</c> or <c>orders.edit_assigned</c>.
    /// </remarks>
    [HttpPut("{id:guid}/boxes/{boxId:guid}/components/{cid:guid}")]
    [Authorize]
    [ProducesResponseType<OrderBoxComponentDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> UpdateComponent(
        Guid id, Guid boxId, Guid cid, [FromBody] UpsertOrderBoxComponentRequest request, CancellationToken ct = default)
    {
        var (order, error) = await LoadOrderWithEditAccessAsync(id, ct);
        if (error is not null) return error;

        if (order!.Status is not (OrderStatus.Draft or OrderStatus.Confirmed))
            return UnprocessableEntity("root", ErrorCode.OrderInvalidStatusTransition,
                "Components can only be updated in Draft or Confirmed status.");

        var component = await db.OrderBoxComponents
            .Include(c => c.CatalogItem).ThenInclude(ci => ci.Group)
            .FirstOrDefaultAsync(c => c.Id == cid && c.OrderBoxId == boxId, ct);
        if (component is null)
            return NotFound(ErrorCode.OrderBoxComponentNotFound, "Component not found.");

        var box = await db.OrderBoxes.FirstOrDefaultAsync(b => b.Id == boxId && b.OrderId == id, ct);
        if (box is null)
            return NotFound(ErrorCode.OrderBoxNotFound, "Box not found.");

        if (request.CatalogItemId != component.CatalogItemId)
        {
            var catalogItem = await db.CatalogItems.Include(ci => ci.Group)
                .FirstOrDefaultAsync(ci => ci.Id == request.CatalogItemId, ct);
            if (catalogItem is null)
                return UnprocessableEntity("catalogItemId", ErrorCode.CatalogItemNotFound, "Catalog item not found.");
            component.CatalogItem = catalogItem;
        }

        component.CatalogItemId = request.CatalogItemId;
        component.Quantity      = request.Quantity;
        await db.SaveChangesAsync(ct);

        return Ok(mapper.Map<OrderBoxComponentDto>(component));
    }

    // ── DELETE /api/orders/{id}/boxes/{boxId}/components/{cid} ───────────────

    /// <summary>Remove a component from a box.</summary>
    /// <remarks>
    /// Allowed only in Draft or Confirmed — otherwise 422 <c>orderInvalidStatusTransition</c>.
    /// Returns 404 <c>orderNotFound</c> or <c>orderBoxComponentNotFound</c>.
    /// Requires <c>orders.edit</c> or <c>orders.edit_assigned</c>.
    /// </remarks>
    [HttpDelete("{id:guid}/boxes/{boxId:guid}/components/{cid:guid}")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> RemoveComponent(
        Guid id, Guid boxId, Guid cid, CancellationToken ct = default)
    {
        var (order, error) = await LoadOrderWithEditAccessAsync(id, ct);
        if (error is not null) return error;

        if (order!.Status is not (OrderStatus.Draft or OrderStatus.Confirmed))
            return UnprocessableEntity("root", ErrorCode.OrderInvalidStatusTransition,
                "Components can only be removed in Draft or Confirmed status.");

        var component = await db.OrderBoxComponents
            .FirstOrDefaultAsync(c => c.Id == cid && c.OrderBoxId == boxId, ct);
        if (component is null)
            return NotFound(ErrorCode.OrderBoxComponentNotFound, "Component not found.");

        await orders.RemoveBoxComponentAsync(component, ct);
        return NoContent();
    }

    // ── POST /api/orders/{id}/assembly-tasks ──────────────────────────────────

    /// <summary>Create an assembly task assigned to one employee, carrying a chosen split of the order's boxes and components.</summary>
    /// <remarks>
    /// Body: <c>CreateAssemblyTaskRequest</c> — <c>assignedToId</c>, <c>boxes[]</c> with
    /// <c>orderBoxId</c> and <c>components[]</c> (<c>catalogItemId</c>, <c>quantity</c>). The task starts as
    /// <c>Pending</c>. One order box may be split across several tasks, but the sum of a catalog item's
    /// quantities over all tasks may not exceed the quantity in the order box.
    /// Errors: 422 <c>orderNotAssembly</c> if the order is not in Assembly, 422 <c>orderBoxNotFound</c> for a box
    /// outside this order, 422 <c>orderBoxComponentNotFound</c> for an item absent from the box, 422
    /// <c>assemblyTaskQuantityExceedsAvailable</c> when the requested quantity exceeds what other tasks left
    /// free. Returns 404 <c>orderNotFound</c>.
    /// Requires <c>orders.edit</c> or <c>orders.edit_assigned</c>.
    /// </remarks>
    [HttpPost("{id:guid}/assembly-tasks")]
    [Authorize]
    [ProducesResponseType<AssemblyTaskDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> CreateAssemblyTask(
        Guid id, [FromBody] CreateAssemblyTaskRequest request, CancellationToken ct = default)
    {
        var (order, error) = await LoadOrderWithEditAccessAsync(id, ct);
        if (error is not null) return error;

        try
        {
            var task = await orders.CreateAssemblyTaskAsync(order!, request, ct);
            if (request.AssignedToId.HasValue)
                await db.Entry(task).Reference(t => t.AssignedTo).LoadAsync(ct);
            foreach (var box in task.Boxes)
            {
                await db.Entry(box).Reference(b => b.OrderBox).LoadAsync(ct);
                foreach (var comp in box.Components)
                    await db.Entry(comp).Reference(c => c.CatalogItem).LoadAsync(ct);
            }
            return CreatedAtAction(nameof(GetById), new { id }, mapper.Map<AssemblyTaskDto>(task));
        }
        catch (ValidationException ex)
        {
            return UnprocessableEntity(ex);
        }
    }

    // ── PUT /api/orders/{id}/assembly-tasks/{taskId} ──────────────────────────

    /// <summary>Reassign an assembly task to another employee.</summary>
    /// <remarks>
    /// Body: <c>UpdateAssemblyTaskRequest</c> — <c>assignedToId</c>; the task's boxes and components are not
    /// changed here. Returns 422 <c>assemblyTaskAlreadyDone</c> once the task is <c>Done</c>, 404
    /// <c>orderNotFound</c> or <c>assemblyTaskNotFound</c>.
    /// Requires <c>orders.edit</c> or <c>orders.edit_assigned</c>.
    /// </remarks>
    [HttpPut("{id:guid}/assembly-tasks/{taskId:guid}")]
    [Authorize]
    [ProducesResponseType<AssemblyTaskDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> UpdateAssemblyTask(
        Guid id, Guid taskId, [FromBody] UpdateAssemblyTaskRequest request, CancellationToken ct = default)
    {
        var (order, error) = await LoadOrderWithEditAccessAsync(id, ct);
        if (error is not null) return error;

        var task = await db.AssemblyTasks
            .Include(t => t.AssignedTo)
            .Include(t => t.Boxes).ThenInclude(b => b.OrderBox)
            .Include(t => t.Boxes).ThenInclude(b => b.Components).ThenInclude(c => c.CatalogItem).ThenInclude(ci => ci.Group)
            .Include(t => t.Boxes).ThenInclude(b => b.Components).ThenInclude(c => c.Fulfillments)
            .FirstOrDefaultAsync(t => t.Id == taskId && t.OrderId == id, ct);
        if (task is null)
            return NotFound(ErrorCode.AssemblyTaskNotFound, "Assembly task not found.");

        try
        {
            await orders.UpdateAssemblyTaskAsync(task, request, ct);
            return Ok(mapper.Map<AssemblyTaskDto>(task));
        }
        catch (ValidationException ex)
        {
            return UnprocessableEntity(ex);
        }
    }

    // ── DELETE /api/orders/{id}/assembly-tasks/{taskId} ───────────────────────

    /// <summary>Delete an assembly task, restoring the inventory of any fulfillments it already had.</summary>
    /// <remarks>
    /// Only possible while the order is in Assembly — otherwise 422 <c>assemblyTaskNotDeletable</c>.
    /// Deletion cascades to the task's boxes, components and fulfillments; picked stock is returned to its source
    /// nodes first. Returns 404 <c>orderNotFound</c> or <c>assemblyTaskNotFound</c>, 409
    /// <c>inventoryWriteConflict</c> when returning that stock loses to concurrent writes — nothing was
    /// written and the request can be repeated.
    /// Requires <c>orders.edit</c> or <c>orders.edit_assigned</c>.
    /// </remarks>
    [HttpDelete("{id:guid}/assembly-tasks/{taskId:guid}")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> DeleteAssemblyTask(Guid id, Guid taskId, CancellationToken ct = default)
    {
        var (order, error) = await LoadOrderWithEditAccessAsync(id, ct);
        if (error is not null) return error;

        var task = await db.AssemblyTasks.FirstOrDefaultAsync(t => t.Id == taskId && t.OrderId == id, ct);
        if (task is null)
            return NotFound(ErrorCode.AssemblyTaskNotFound, "Assembly task not found.");

        try
        {
            await orders.DeleteAssemblyTaskAsync(task, ct);
            return NoContent();
        }
        catch (ValidationException ex)
        {
            return UnprocessableEntity(ex);
        }
        catch (InventoryWriteConflictException)
        {
            return Conflict(ErrorCode.InventoryWriteConflict,
                "Stock for this item was changed concurrently; nothing was written.");
        }
    }

    // ── PUT /api/orders/{id}/assembly-tasks/{taskId}/status ───────────────────

    /// <summary>Move an assembly task to another status.</summary>
    /// <remarks>
    /// Body: <c>TransitionAssemblyTaskStatusRequest</c> — <c>targetStatus</c>. Allowed transitions:
    /// <list type="bullet">
    ///   <item>Pending → InProgress</item>
    ///   <item>InProgress → Done</item>
    ///   <item>InProgress → Pending</item>
    ///   <item>Done → InProgress</item>
    /// </list>
    /// Anything else is 422 <c>orderInvalidStatusTransition</c>. Only allowed while the order itself is
    /// <c>Assembly</c> or <c>Assembled</c> — otherwise 422 <c>orderNotAssembly</c>. Completing the last remaining
    /// task of an Assembly-status order moves the order itself to <c>Assembled</c> only if every component of
    /// every task is fully fulfilled (not just every task Done) — a task can still be marked Done with components
    /// left unfulfilled, but the order then stays in <c>Assembly</c> until the shortfall is fulfilled and the
    /// check re-runs on a later task transition. Rolling a task back out of Done while the order is
    /// <c>Assembled</c> moves the order back to <c>Assembly</c>.
    /// Returns 404 <c>orderNotFound</c> or <c>assemblyTaskNotFound</c>.
    /// Requires <c>orders.assemble_assigned</c>, <c>orders.edit</c> or <c>orders.edit_assigned</c>, plus an
    /// assignment to the order's warehouse in every case (403 <c>orderNotAssignedToWarehouse</c>).
    /// </remarks>
    [HttpPut("{id:guid}/assembly-tasks/{taskId:guid}/status")]
    [Authorize]
    [ProducesResponseType<AssemblyTaskDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> TransitionTaskStatus(
        Guid id, Guid taskId, [FromBody] TransitionAssemblyTaskStatusRequest request, CancellationToken ct = default)
    {
        var (order, error) = await LoadOrderWithAssembleAccessAsync(id, ct);
        if (error is not null) return error;

        var task = await db.AssemblyTasks
            .Include(t => t.AssignedTo)
            .Include(t => t.Boxes).ThenInclude(b => b.OrderBox)
            .Include(t => t.Boxes).ThenInclude(b => b.Components).ThenInclude(c => c.CatalogItem).ThenInclude(ci => ci.Group)
            .Include(t => t.Boxes).ThenInclude(b => b.Components).ThenInclude(c => c.Fulfillments)
            .FirstOrDefaultAsync(t => t.Id == taskId && t.OrderId == id, ct);
        if (task is null)
            return NotFound(ErrorCode.AssemblyTaskNotFound, "Assembly task not found.");

        try
        {
            await orders.TransitionTaskStatusAsync(task, request.TargetStatus, order!, ct);
            return Ok(mapper.Map<AssemblyTaskDto>(task));
        }
        catch (ValidationException ex)
        {
            return UnprocessableEntity(ex);
        }
    }

    // ── PUT .../boxes/{tbid}/components/{cid} (admin edit during assembly) ────

    /// <summary>Change the quantity a task must assemble for one of its box components.</summary>
    /// <remarks>
    /// Body: <c>UpdateAssemblyTaskBoxComponentRequest</c> — <c>quantity</c>. Admin-side correction of the split
    /// while assembly is running: allowed only in Assembly status, otherwise 422 <c>orderNotAssembly</c>.
    /// The new quantity may not exceed what the order box has left after the other tasks' allocations (this
    /// task's own current value is excluded from that sum) — 422 <c>assemblyTaskQuantityExceedsAvailable</c>.
    /// Returns 404 <c>orderNotFound</c> or <c>assemblyTaskBoxComponentNotFound</c>.
    /// Requires <c>orders.edit</c> or <c>orders.edit_assigned</c>.
    /// </remarks>
    [HttpPut("{id:guid}/assembly-tasks/{taskId:guid}/boxes/{tbid:guid}/components/{cid:guid}")]
    [Authorize]
    [ProducesResponseType<AssemblyTaskBoxComponentDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> UpdateTaskBoxComponent(
        Guid id, Guid taskId, Guid tbid, Guid cid,
        [FromBody] UpdateAssemblyTaskBoxComponentRequest request,
        CancellationToken ct = default)
    {
        var (order, error) = await LoadOrderWithEditAccessAsync(id, ct);
        if (error is not null) return error;

        if (order!.Status != OrderStatus.Assembly)
            return UnprocessableEntity("root", ErrorCode.OrderNotAssembly,
                "Task components can only be edited during Assembly.");

        var component = await db.AssemblyTaskBoxComponents
            .Include(c => c.AssemblyTaskBox)
            .Include(c => c.CatalogItem).ThenInclude(ci => ci.Group)
            .Include(c => c.Fulfillments)
            .FirstOrDefaultAsync(c => c.Id == cid && c.AssemblyTaskBoxId == tbid, ct);
        if (component is null)
            return NotFound(ErrorCode.AssemblyTaskBoxComponentNotFound, "Task box component not found.");

        try
        {
            await orders.UpdateTaskBoxComponentAsync(component, request.Quantity, ct);
        }
        catch (ValidationException ex)
        {
            return UnprocessableEntity(ex);
        }

        return Ok(mapper.Map<AssemblyTaskBoxComponentDto>(component));
    }

    // ── GET .../boxes/{tbid}/components/{cid}/move-targets (assembler only) ───

    /// <summary>List the order boxes a task component can be moved into — every box of the order except the one it currently sits in.</summary>
    /// <remarks>
    /// Only available in Assembly status (422 <c>orderNotAssembly</c>) and only to holders of
    /// <c>orders.assemble_assigned</c> assigned to the order's warehouse — <c>orders.edit</c> alone gets 403.
    /// Returns 404 <c>orderNotFound</c> or <c>assemblyTaskBoxComponentNotFound</c>.
    /// </remarks>
    [HttpGet("{id:guid}/assembly-tasks/{taskId:guid}/boxes/{tbid:guid}/components/{cid:guid}/move-targets")]
    [Authorize]
    [ProducesResponseType<IReadOnlyList<OrderBoxDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> GetTaskMoveTargets(
        Guid id, Guid taskId, Guid tbid, Guid cid, CancellationToken ct = default)
    {
        var (order, error) = await LoadOrderWithAssembleAccessAsync(id, ct);
        if (error is not null) return error;

        if (!User.HasClaim("permission", Permissions.Orders.AssembleAssigned))
            return Forbidden();

        if (order!.Status != OrderStatus.Assembly)
            return UnprocessableEntity("root", ErrorCode.OrderNotAssembly,
                "Moving components is only available during Assembly.");

        var component = await db.AssemblyTaskBoxComponents
            .Include(c => c.AssemblyTaskBox).ThenInclude(b => b.OrderBox)
            .FirstOrDefaultAsync(c => c.Id == cid && c.AssemblyTaskBoxId == tbid && c.AssemblyTaskBox.AssemblyTaskId == taskId, ct);
        if (component is null)
            return NotFound(ErrorCode.AssemblyTaskBoxComponentNotFound, "Task box component not found.");

        var targets = await orders.GetTaskMoveTargetsAsync(component, ct);
        return Ok(mapper.Map<IReadOnlyList<OrderBoxDto>>(targets));
    }

    // ── POST .../boxes/{tbid}/components/{cid}/move (assembler only) ──────────

    /// <summary>Move part or all of this task's allocation of a component into another box, or into a newly created one.</summary>
    /// <remarks>
    /// Body: <c>MoveTaskBoxComponentRequest</c> — <c>quantity</c> plus exactly one of <c>targetBoxId</c> or
    /// <c>newBoxLabel</c> (422 <c>validationError</c> if both or neither are given).
    /// The task's own component split and the order's overall composition are updated by the same amount at
    /// once; other tasks holding the same box/item are untouched. The movable amount is this task's unfulfilled
    /// remainder (<c>quantity</c> minus already confirmed fulfillments), not the box's total — 422
    /// <c>outOfRange</c> otherwise. A task box left empty by the move is deleted.
    /// Further errors: 422 <c>orderBoxNotFound</c> for a target box that does not exist or belongs to another
    /// order, 422 <c>validationError</c> if the target equals the source box, 422 <c>orderNotAssembly</c>
    /// outside Assembly status, 404 <c>orderNotFound</c> or <c>assemblyTaskBoxComponentNotFound</c>.
    /// Requires <c>orders.assemble_assigned</c> and an assignment to the order's warehouse; <c>orders.edit</c>
    /// alone gets 403.
    /// </remarks>
    [HttpPost("{id:guid}/assembly-tasks/{taskId:guid}/boxes/{tbid:guid}/components/{cid:guid}/move")]
    [Authorize]
    [ProducesResponseType<OrderDetailsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> MoveTaskComponent(
        Guid id, Guid taskId, Guid tbid, Guid cid, [FromBody] MoveTaskBoxComponentRequest request, CancellationToken ct = default)
    {
        var (order, error) = await LoadOrderWithAssembleAccessAsync(id, ct);
        if (error is not null) return error;

        if (!User.HasClaim("permission", Permissions.Orders.AssembleAssigned))
            return Forbidden();

        if (order!.Status != OrderStatus.Assembly)
            return UnprocessableEntity("root", ErrorCode.OrderNotAssembly,
                "Moving components is only available during Assembly.");

        var component = await db.AssemblyTaskBoxComponents
            .Include(c => c.AssemblyTaskBox).ThenInclude(b => b.OrderBox)
            .Include(c => c.AssemblyTaskBox).ThenInclude(b => b.Components)
            .Include(c => c.Fulfillments)
            .FirstOrDefaultAsync(c => c.Id == cid && c.AssemblyTaskBoxId == tbid && c.AssemblyTaskBox.AssemblyTaskId == taskId, ct);
        if (component is null)
            return NotFound(ErrorCode.AssemblyTaskBoxComponentNotFound, "Task box component not found.");

        try
        {
            await orders.MoveTaskBoxComponentAsync(component, request, ct);
        }
        catch (ValidationException ex)
        {
            return UnprocessableEntity(ex);
        }

        var full = await LoadOrderDetailsAsync(id, ct);
        return Ok(mapper.Map<OrderDetailsDto>(full));
    }

    // ── POST .../fulfillments ─────────────────────────────────────────────────

    /// <summary>Record that a task component was picked, deducting the stock from the warehouse immediately.</summary>
    /// <remarks>
    /// Body: <c>AddFulfillmentRequest</c> — exactly one of three shapes must be filled:
    /// <list type="bullet">
    ///   <item>Standard — <c>sourceNodeId</c> + <c>quantity</c></item>
    ///   <item>Unit — <c>unitInventoryItemId</c></item>
    ///   <item>Bundle — <c>bundleComponents[]</c>, one entry per resolved leaf</item>
    /// </list>
    /// Anything else is 422 <c>assemblyFulfillmentInvalidType</c>. For a <c>Variation</c> component the client
    /// must also send <c>resolvedCatalogItemId</c> (422 <c>required</c>), which is verified to be a member of the
    /// variation; in the Unit case it is taken from the instance instead.
    /// Inventory moves on write: Standard decrements the node's count, Unit detaches the instance. A Unit or
    /// Bundle fulfillment always counts as exactly one unit of progress, so N identical bundles need N
    /// fulfillments.
    /// Errors: 422 <c>assemblyComponentAlreadyFulfilled</c> when the component is already complete (re-checked
    /// against fresh state to absorb duplicate submits), 422 <c>insufficientInventory</c>,
    /// <c>unitInventoryItemNotFound</c>, <c>inventoryItemNodeMismatch</c>, <c>catalogItemNotFound</c>,
    /// 422 <c>orderNotAssembly</c> outside Assembly status, 404 <c>orderNotFound</c> or
    /// <c>assemblyTaskBoxComponentNotFound</c>, 409 <c>inventoryWriteConflict</c> when concurrent stock writes
    /// outlast the retry budget — nothing was written and the request can be repeated.
    /// Requires <c>orders.assemble_assigned</c>, <c>orders.edit</c> or <c>orders.edit_assigned</c>, plus an
    /// assignment to the order's warehouse in every case.
    /// </remarks>
    [HttpPost("{id:guid}/assembly-tasks/{taskId:guid}/boxes/{tbid:guid}/components/{cid:guid}/fulfillments")]
    [Authorize]
    [ProducesResponseType<AssemblyFulfillmentDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> AddFulfillment(
        Guid id, Guid taskId, Guid tbid, Guid cid,
        [FromBody] AddFulfillmentRequest request,
        CancellationToken ct = default)
    {
        var (order, error) = await LoadOrderWithAssembleAccessAsync(id, ct);
        if (error is not null) return error;

        if (order!.Status != OrderStatus.Assembly)
            return UnprocessableEntity("root", ErrorCode.OrderNotAssembly,
                "Fulfillments can only be added during Assembly.");

        var component = await db.AssemblyTaskBoxComponents
            .Include(c => c.CatalogItem).ThenInclude(ci => ci.Group)
            .Include(c => c.Fulfillments)
            .FirstOrDefaultAsync(c => c.Id == cid && c.AssemblyTaskBoxId == tbid, ct);
        if (component is null)
            return NotFound(ErrorCode.AssemblyTaskBoxComponentNotFound, "Task box component not found.");

        try
        {
            var fulfillment = await orders.AddFulfillmentAsync(component, request, GetCurrentUserId(), ct);
            var nodeById = await LoadWarehouseNodesAsync([order.WarehouseId], ct);
            return CreatedAtAction(nameof(GetById), new { id },
                mapper.Map<AssemblyFulfillmentDto>(fulfillment, opts => opts.Items["nodeById"] = nodeById));
        }
        catch (ValidationException ex)
        {
            return UnprocessableEntity(ex);
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
        catch (UnitInventoryItemNotFoundException)
        {
            return UnprocessableEntity("unitInventoryItemId", ErrorCode.UnitInventoryItemNotFound,
                "Unit inventory item not found.");
        }
        catch (InventoryItemNodeMismatchException)
        {
            return UnprocessableEntity("root", ErrorCode.InventoryItemNodeMismatch,
                "Item is not at the expected storage node.");
        }
        catch (AssemblyComponentAlreadyFulfilledException)
        {
            return UnprocessableEntity("root", ErrorCode.AssemblyComponentAlreadyFulfilled,
                "This component is already fully fulfilled.");
        }
    }

    // ── DELETE .../fulfillments/{fid} ─────────────────────────────────────────

    /// <summary>Undo a fulfillment, returning the picked stock to its source node.</summary>
    /// <remarks>
    /// Standard rows increment the node count back, Unit rows reattach the instance, Bundle rows restore every
    /// leaf. No status guard: this works whatever status the order is in.
    /// Returns 404 <c>orderNotFound</c> or <c>assemblyFulfillmentNotFound</c> (the fulfillment must belong to the
    /// component, task box and task named in the route), 409 <c>inventoryWriteConflict</c> when concurrent stock
    /// writes outlast the retry budget — nothing was returned and the request can be repeated.
    /// Requires <c>orders.assemble_assigned</c>, <c>orders.edit</c> or <c>orders.edit_assigned</c>, plus an
    /// assignment to the order's warehouse in every case.
    /// </remarks>
    [HttpDelete("{id:guid}/assembly-tasks/{taskId:guid}/boxes/{tbid:guid}/components/{cid:guid}/fulfillments/{fid:guid}")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RemoveFulfillment(
        Guid id, Guid taskId, Guid tbid, Guid cid, Guid fid, CancellationToken ct = default)
    {
        var (order, error) = await LoadOrderWithAssembleAccessAsync(id, ct);
        if (error is not null) return error;

        var fulfillment = await db.AssemblyFulfillments
            .Include(f => f.TaskBoxComponent).ThenInclude(c => c.CatalogItem).ThenInclude(ci => ci.Group)
            .Include(f => f.BundleComponents)
            .FirstOrDefaultAsync(f => f.Id == fid
                && f.TaskBoxComponent.Id == cid
                && f.TaskBoxComponent.AssemblyTaskBox.Id == tbid
                && f.TaskBoxComponent.AssemblyTaskBox.AssemblyTaskId == taskId, ct);

        if (fulfillment is null)
            return NotFound(ErrorCode.AssemblyFulfillmentNotFound, "Fulfillment not found.");

        try
        {
            await orders.RemoveFulfillmentAsync(fulfillment, ct);
            return NoContent();
        }
        catch (ValidationException ex)
        {
            return UnprocessableEntity(ex);
        }
        catch (InventoryWriteConflictException)
        {
            return Conflict(ErrorCode.InventoryWriteConflict,
                "Stock for this item was changed concurrently; nothing was written.");
        }
    }

    // ── POST /api/orders/assembly-tasks/batch-fulfill ─────────────────────────

    /// <summary>Record many fulfillments across several orders and tasks in one request, with partial-success semantics.</summary>
    /// <remarks>
    /// Body: <c>BatchFulfillRequest</c> — <c>items[]</c> (<c>orderId</c>, <c>taskId</c>, <c>taskBoxId</c>,
    /// <c>componentId</c>, <c>fulfillment</c>) and <c>autoCompleteTasks</c>. Items are processed grouped by
    /// order; the same <c>componentId</c> may appear several times, which is how N identical bundles are picked.
    /// Always answers 200 with <c>BatchFulfillResponse</c>: each failure lands in <c>failedItems</c> as
    /// <c>{ orderId, componentId, catalogItemName, error }</c> carrying the real error code
    /// (<c>orderNotFound</c>, <c>orderNotAssignedToWarehouse</c>, <c>orderNotAssembly</c>,
    /// <c>assemblyTaskBoxComponentNotFound</c>, <c>insufficientInventory</c>, <c>unitInventoryItemNotFound</c>,
    /// <c>inventoryItemNodeMismatch</c>, <c>assemblyComponentAlreadyFulfilled</c>, <c>inventoryWriteConflict</c>,
    /// …), while successful items are
    /// committed and stay committed. There is no overall transaction.
    /// With <c>autoCompleteTasks: false</c> task statuses are never touched and <c>completedTaskIds</c> comes back
    /// empty. With <c>true</c>, every touched task is advanced Pending → InProgress, and InProgress → Done only
    /// when all of its components are fully fulfilled; only genuinely completed tasks are listed in
    /// <c>completedTaskIds</c>. That step is best-effort — failures there are swallowed and do not fail the
    /// request. Completing the last task still auto-moves the order to <c>Assembled</c>, but only once every
    /// component of every task in the order is fully fulfilled — see the assembly-task status endpoint.
    /// 403 is returned only for the request as a whole, when neither <c>orders.assemble_assigned</c> nor
    /// <c>orders.edit</c> / <c>orders.edit_assigned</c> is held; warehouse assignment is then checked per order.
    /// The route carries no id, so realtime change events are published explicitly for each affected order.
    /// </remarks>
    [HttpPost("assembly-tasks/batch-fulfill")]
    [Authorize]
    [ProducesResponseType<BatchFulfillResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> BatchFulfill(
        [FromBody] BatchFulfillRequest request, CancellationToken ct = default)
    {
        var canAssemble = User.HasClaim("permission", Permissions.Orders.AssembleAssigned);
        var canEdit     = User.HasClaim("permission", Permissions.Orders.Edit)
                       || User.HasClaim("permission", Permissions.Orders.EditAssigned);

        if (!canAssemble && !canEdit)
            return Forbidden();

        var assignedWarehouseIds = await scope.GetAssignedWarehouseIdsAsync(User, ct);
        if (assignedWarehouseIds is null)
            return Unauthorized(ErrorCode.TokenInvalid, "Invalid token.");

        var completedTaskIds = new List<string>();
        var failedItems      = new List<BatchFulfillFailedItem>();
        var changedOrderIds  = new HashSet<Guid>();

        void Fail(BatchFulfillItemRequest item, ErrorCode code, string message,
            IReadOnlyDictionary<string, object>? args = null, string catalogItemName = "") =>
            failedItems.Add(new BatchFulfillFailedItem
            {
                OrderId = item.OrderId,
                ComponentId = item.ComponentId,
                CatalogItemName = catalogItemName,
                Error = AppProblems.MakeError(code, message, args),
            });

        // Process items grouped by order to avoid redundant DB lookups
        var itemsByOrder = request.Items.GroupBy(i => i.OrderId).ToList();

        foreach (var orderGroup in itemsByOrder)
        {
            var order = await db.Orders.FirstOrDefaultAsync(o => o.Id == orderGroup.Key, ct);
            if (order is null)
            {
                foreach (var item in orderGroup)
                    Fail(item, ErrorCode.OrderNotFound, "Order not found.");
                continue;
            }

            if (!assignedWarehouseIds.Contains(order.WarehouseId))
            {
                foreach (var item in orderGroup)
                    Fail(item, ErrorCode.OrderNotAssignedToWarehouse, "Not assigned to this order's warehouse.");
                continue;
            }

            if (order.Status != OrderStatus.Assembly)
            {
                foreach (var item in orderGroup)
                    Fail(item, ErrorCode.OrderNotAssembly, "Order is not in Assembly status.");
                continue;
            }

            var attemptedTaskIds = new HashSet<Guid>();

            foreach (var item in orderGroup)
            {
                var itemName = "";
                try
                {
                    var component = await db.AssemblyTaskBoxComponents
                        .Include(c => c.CatalogItem).ThenInclude(ci => ci.Group)
                        .Include(c => c.Fulfillments)
                        .FirstOrDefaultAsync(c => c.Id == item.ComponentId && c.AssemblyTaskBoxId == item.TaskBoxId, ct);

                    if (component is null)
                    {
                        Fail(item, ErrorCode.AssemblyTaskBoxComponentNotFound, "Component not found.");
                        continue;
                    }

                    itemName = component.CatalogItem.FullName;

                    await orders.AddFulfillmentAsync(component, item.Fulfillment, GetCurrentUserId(), ct);
                    attemptedTaskIds.Add(item.TaskId);
                    changedOrderIds.Add(order.Id);
                }
                catch (ValidationException ex)
                {
                    Fail(item, ex.ErrorCode, ex.Message, catalogItemName: itemName);
                }
                catch (InventoryWriteConflictException)
                {
                    Fail(item, ErrorCode.InventoryWriteConflict,
                        "Stock for this item was changed concurrently; nothing was written.",
                        catalogItemName: itemName);
                }
                catch (InsufficientInventoryException ex)
                {
                    Fail(item, ErrorCode.InsufficientInventory,
                        $"Insufficient inventory at node '{ex.NodeId}': requested {ex.Requested}, available {ex.Available}.",
                        ex.ToArgs(), itemName);
                }
                catch (UnitInventoryItemNotFoundException)
                {
                    Fail(item, ErrorCode.UnitInventoryItemNotFound, "Unit inventory item not found.", catalogItemName: itemName);
                }
                catch (InventoryItemNodeMismatchException)
                {
                    Fail(item, ErrorCode.InventoryItemNodeMismatch, "Item is not at the expected storage node.", catalogItemName: itemName);
                }
                catch (AssemblyComponentAlreadyFulfilledException)
                {
                    Fail(item, ErrorCode.AssemblyComponentAlreadyFulfilled, "Component is already fully fulfilled.", catalogItemName: itemName);
                }
            }

            // Mass-assembly only: advance touched tasks, completing just the fully fulfilled ones
            if (!request.AutoCompleteTasks)
                continue;

            foreach (var taskId in attemptedTaskIds)
            {
                try
                {
                    var fullOrder = await db.Orders
                        .Include(o => o.AssemblyTasks)
                        .FirstOrDefaultAsync(o => o.Id == order.Id, ct);
                    if (fullOrder is null) continue;

                    var task = await db.AssemblyTasks
                        .FirstOrDefaultAsync(t => t.Id == taskId && t.OrderId == order.Id, ct);
                    if (task is null) continue;

                    if (task.Status == AssemblyTaskStatus.Pending)
                        await orders.TransitionTaskStatusAsync(task, AssemblyTaskStatus.InProgress, fullOrder, ct);

                    if (!await orders.IsTaskFullyFulfilledAsync(taskId, ct))
                        continue;

                    if (task.Status == AssemblyTaskStatus.InProgress)
                        await orders.TransitionTaskStatusAsync(task, AssemblyTaskStatus.Done, fullOrder, ct);

                    completedTaskIds.Add(taskId.ToString());
                }
                catch (Exception)
                {
                    // Auto-complete is best-effort; partial success is acceptable
                }
            }
        }

        foreach (var orderId in changedOrderIds)
            await realtime.PublishEntityChangedAsync(AppEntityType.Order, orderId, User, ct);

        return Ok(new BatchFulfillResponse
        {
            CompletedTaskIds = completedTaskIds,
            FailedItems      = failedItems,
        });
    }
}
