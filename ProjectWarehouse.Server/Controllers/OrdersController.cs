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
using ProjectWarehouse.Server.Models;
using ProjectWarehouse.Server.Models.Orders;
using ProjectWarehouse.Server.Services;

namespace ProjectWarehouse.Server.Controllers;

[Route("api/orders")]
public class OrdersController(
    ApplicationDbContext db,
    IMapper mapper,
    IOrderService orders,
    ICatalogService catalog) : AppControllerBase
{
    // ── Base query helpers ────────────────────────────────────────────────────

    private IQueryable<Order> BaseQuery() =>
        db.Orders
            .Include(o => o.Warehouse)
            .Include(o => o.CreatedBy);

    private IQueryable<Order> DetailsQuery() =>
        BaseQuery()
            .Include(o => o.Boxes).ThenInclude(b => b.Components).ThenInclude(c => c.CatalogItem).ThenInclude(ci => ci.Group)
            .Include(o => o.AssemblyTasks).ThenInclude(t => t.AssignedTo)
            .Include(o => o.AssemblyTasks).ThenInclude(t => t.Boxes).ThenInclude(tb => tb.OrderBox)
            .Include(o => o.AssemblyTasks).ThenInclude(t => t.Boxes)
                .ThenInclude(tb => tb.Components).ThenInclude(c => c.CatalogItem).ThenInclude(ci => ci.Group)
            .Include(o => o.AssemblyTasks).ThenInclude(t => t.Boxes)
                .ThenInclude(tb => tb.Components).ThenInclude(c => c.Fulfillments)
                .ThenInclude(f => f.BundleComponents).ThenInclude(bc => bc.CatalogItem).ThenInclude(ci => ci.Group);

    // ── Access helpers ────────────────────────────────────────────────────────

    private async Task<(bool canView, bool canViewAssigned, HashSet<Guid>? assignedIds)>
        GetViewAccessAsync(CancellationToken ct)
    {
        var canView         = User.HasClaim("permission", Permissions.Orders.View);
        var canViewAssigned = User.HasClaim("permission", Permissions.Orders.ViewAssigned);

        if (!canView && !canViewAssigned)
            return (false, false, null);

        HashSet<Guid>? assignedIds = null;
        if (!canView)
            assignedIds = await GetCurrentUserAssignedWarehouseIdsAsync(db, ct);

        return (canView, canViewAssigned, assignedIds);
    }

    private async Task<(Order? order, IActionResult? error)> LoadOrderWithEditAccessAsync(
        Guid id, CancellationToken ct, bool fullDetails = false)
    {
        var canEdit         = User.HasClaim("permission", Permissions.Orders.Edit);
        var canEditAssigned = User.HasClaim("permission", Permissions.Orders.EditAssigned);

        if (!canEdit && !canEditAssigned)
            return (null, Forbidden());

        var query  = fullDetails ? DetailsQuery() : BaseQuery();
        var order  = await query.FirstOrDefaultAsync(o => o.Id == id, ct);
        if (order is null)
            return (null, NotFound(ErrorCode.OrderNotFound, "Order not found."));

        if (canEditAssigned && !canEdit)
        {
            var assignedIds = await GetCurrentUserAssignedWarehouseIdsAsync(db, ct);
            if (assignedIds is null)
                return (null, Unauthorized(ErrorCode.TokenInvalid, "Invalid token."));
            if (!assignedIds.Contains(order.WarehouseId))
                return (null, Forbidden(ErrorCode.OrderNotAssignedToWarehouse,
                    "You are not assigned to the warehouse of this order."));
        }

        return (order, null);
    }

    private async Task<(Order? order, IActionResult? error)> LoadOrderWithAssembleAccessAsync(
        Guid id, CancellationToken ct, bool fullDetails = false)
    {
        var canAssemble = User.HasClaim("permission", Permissions.Orders.AssembleAssigned);
        var canEdit     = User.HasClaim("permission", Permissions.Orders.Edit)
                       || User.HasClaim("permission", Permissions.Orders.EditAssigned);

        if (!canAssemble && !canEdit)
            return (null, Forbidden());

        var query = fullDetails ? DetailsQuery() : BaseQuery();
        var order = await query.FirstOrDefaultAsync(o => o.Id == id, ct);
        if (order is null)
            return (null, NotFound(ErrorCode.OrderNotFound, "Order not found."));

        var assignedIds = await GetCurrentUserAssignedWarehouseIdsAsync(db, ct);
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
        [FromQuery] OrderSortBy sortBy = OrderSortBy.Number,
        [FromQuery] SortOrder sortOrder = SortOrder.Desc,
        CancellationToken ct = default)
    {
        var (canView, canViewAssigned, assignedIds) = await GetViewAccessAsync(ct);
        if (!canView && !canViewAssigned)
            return Forbidden();

        if (!canView && assignedIds is null)
            return Unauthorized(ErrorCode.TokenInvalid, "Invalid token.");

        var baseQuery = db.Orders
            .Include(o => o.Warehouse)
            .Include(o => o.CreatedBy)
            .Include(o => o.Boxes).ThenInclude(b => b.Components)
            .Where(o => warehouseId == null || o.WarehouseId == warehouseId)
            .Where(o => type == null || o.Type == type)
            .Where(o => status == null || o.Status == status)
            .Where(o => assignedIds == null || assignedIds.Contains(o.WarehouseId))
            .WhereMatchesSearch(o => o.SearchString, searchString);

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

    [HttpGet("assembly")]
    [Authorize]
    [ProducesResponseType<List<OrderDetailsDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllAssembly(CancellationToken ct = default)
    {
        var (canView, canViewAssigned, assignedIds) = await GetViewAccessAsync(ct);
        var canAssemble = User.HasClaim("permission", Permissions.Orders.AssembleAssigned);

        if (!canView && !canViewAssigned && !canAssemble)
            return Forbidden();

        if (!canView && assignedIds is null)
            return Unauthorized(ErrorCode.TokenInvalid, "Invalid token.");

        var userId = GetCurrentUserId();
        if (userId is null)
            return Unauthorized(ErrorCode.TokenInvalid, "Invalid token.");

        var query = db.Orders
            .Include(o => o.Warehouse)
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
            .Where(o => o.Status == OrderStatus.Assembly)
            .Where(o => o.AssemblyTasks.Any(t => t.AssignedToId == userId))
            .AsSplitQuery();

        if (!canView && assignedIds is not null)
            query = query.Where(o => assignedIds.Contains(o.WarehouseId));

        var result = await query.ToListAsync(ct);
        var dtos = mapper.Map<List<OrderDetailsDto>>(result);

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

    [HttpGet("{id:guid}")]
    [Authorize]
    [ProducesResponseType<OrderDetailsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct = default)
    {
        var (canView, canViewAssigned, assignedIds) = await GetViewAccessAsync(ct);
        if (!canView && !canViewAssigned)
            return Forbidden();

        if (!canView && assignedIds is null)
            return Unauthorized(ErrorCode.TokenInvalid, "Invalid token.");

        var order = await DetailsQuery().FirstOrDefaultAsync(o => o.Id == id, ct);
        if (order is null)
            return NotFound(ErrorCode.OrderNotFound, "Order not found.");

        if (assignedIds is not null && !assignedIds.Contains(order.WarehouseId))
            return Forbidden();

        return Ok(mapper.Map<OrderDetailsDto>(order));
    }

    // ── POST /api/orders/direct ───────────────────────────────────────────────

    [HttpPost("direct")]
    [Authorize]
    [ProducesResponseType<OrderDetailsDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> CreateDirect([FromBody] CreateDirectOrderRequest request, CancellationToken ct = default)
    {
        var canEdit         = User.HasClaim("permission", Permissions.Orders.Edit);
        var canEditAssigned = User.HasClaim("permission", Permissions.Orders.EditAssigned);

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
                return Forbidden(ErrorCode.OrderNotAssignedToWarehouse,
                    "You are not assigned to the warehouse of this order.");
        }

        var order = await orders.CreateDirectOrderAsync(request, GetCurrentUserId(), ct);

        var full = await LoadOrderDetailsAsync(order.Id, ct);
        return CreatedAtAction(nameof(GetById), new { id = order.Id }, mapper.Map<OrderDetailsDto>(full));
    }

    // ── PUT /api/orders/{id} ──────────────────────────────────────────────────

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

    [HttpPut("{id:guid}/status")]
    [Authorize]
    [ProducesResponseType<OrderDetailsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status404NotFound)]
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

        var full = await LoadOrderDetailsAsync(id, ct);
        return Ok(mapper.Map<OrderDetailsDto>(full));
    }

    // ── POST /api/orders/{id}/self-assign ─────────────────────────────────────

    [HttpPost("{id:guid}/self-assign")]
    [Authorize]
    [ProducesResponseType<OrderDetailsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> SelfAssign(Guid id, CancellationToken ct = default)
    {
        if (!User.HasClaim("permission", Permissions.Orders.SelfAssign))
            return Forbidden();

        var assignedIds = await GetCurrentUserAssignedWarehouseIdsAsync(db, ct);
        if (assignedIds is null)
            return Unauthorized(ErrorCode.TokenInvalid, "Invalid token.");

        var order = await DetailsQuery().FirstOrDefaultAsync(o => o.Id == id, ct);
        if (order is null)
            return NotFound(ErrorCode.OrderNotFound, "Order not found.");

        if (!assignedIds.Contains(order.WarehouseId))
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

    // ── POST /api/orders/{id}/boxes ───────────────────────────────────────────

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
            : await GetCurrentUserAssignedWarehouseIdsAsync(db, ct);

        if (assignedIds is not null && !assignedIds.Contains(order.WarehouseId))
            return Forbidden(ErrorCode.OrderNotAssignedToWarehouse, "You are not assigned to this order's warehouse.");

        var box = await orders.AddBoxAsync(order, request, ct);
        return CreatedAtAction(nameof(GetById), new { id = order.Id }, mapper.Map<OrderBoxDto>(box));
    }

    // ── PUT /api/orders/{id}/boxes/{boxId} ────────────────────────────────────

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

    [HttpDelete("{id:guid}/assembly-tasks/{taskId:guid}")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
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
    }

    // ── PUT /api/orders/{id}/assembly-tasks/{taskId}/status ───────────────────

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
            .Include(c => c.CatalogItem)
            .Include(c => c.Fulfillments)
            .FirstOrDefaultAsync(c => c.Id == cid && c.AssemblyTaskBoxId == tbid, ct);
        if (component is null)
            return NotFound(ErrorCode.AssemblyTaskBoxComponentNotFound, "Task box component not found.");

        try
        {
            var fulfillment = await orders.AddFulfillmentAsync(component, request, ct);
            return CreatedAtAction(nameof(GetById), new { id }, mapper.Map<AssemblyFulfillmentDto>(fulfillment));
        }
        catch (ValidationException ex)
        {
            return UnprocessableEntity(ex);
        }
        catch (InsufficientInventoryException ex)
        {
            return UnprocessableEntity("root", ErrorCode.InsufficientInventory,
                $"Insufficient inventory at node '{ex.NodeId}': requested {ex.Requested}, available {ex.Available}.");
        }
        catch (UnitInventoryItemNotFoundException)
        {
            return UnprocessableEntity("unitInventoryItemId", ErrorCode.UnitInventoryItemNotFound,
                "Unit inventory item not found.");
        }
        catch (InventoryItemNodeMismatchException)
        {
            return UnprocessableEntity("root", ErrorCode.WriteoffItemNotFound,
                "Item is not at the expected storage node.");
        }
        catch (AssemblyComponentAlreadyFulfilledException)
        {
            return UnprocessableEntity("root", ErrorCode.AssemblyComponentAlreadyFulfilled,
                "This component is already fully fulfilled.");
        }
    }

    // ── DELETE .../fulfillments/{fid} ─────────────────────────────────────────

    [HttpDelete("{id:guid}/assembly-tasks/{taskId:guid}/boxes/{tbid:guid}/components/{cid:guid}/fulfillments/{fid:guid}")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveFulfillment(
        Guid id, Guid taskId, Guid tbid, Guid cid, Guid fid, CancellationToken ct = default)
    {
        var (order, error) = await LoadOrderWithAssembleAccessAsync(id, ct);
        if (error is not null) return error;

        var fulfillment = await db.AssemblyFulfillments
            .Include(f => f.TaskBoxComponent).ThenInclude(c => c.CatalogItem)
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
    }

    // ── POST /api/orders/assembly-tasks/batch-fulfill ─────────────────────────

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

        var assignedWarehouseIds = await GetCurrentUserAssignedWarehouseIdsAsync(db, ct);
        if (assignedWarehouseIds is null)
            return Unauthorized(ErrorCode.TokenInvalid, "Invalid token.");

        var completedTaskIds = new List<string>();
        var failedItems      = new List<BatchFulfillFailedItem>();

        // Process items grouped by order to avoid redundant DB lookups
        var itemsByOrder = request.Items.GroupBy(i => i.OrderId).ToList();

        foreach (var orderGroup in itemsByOrder)
        {
            var order = await db.Orders.FirstOrDefaultAsync(o => o.Id == orderGroup.Key, ct);
            if (order is null)
            {
                foreach (var item in orderGroup)
                    failedItems.Add(new BatchFulfillFailedItem { OrderId = item.OrderId, ComponentId = item.ComponentId, Error = "Order not found." });
                continue;
            }

            if (!assignedWarehouseIds.Contains(order.WarehouseId))
            {
                foreach (var item in orderGroup)
                    failedItems.Add(new BatchFulfillFailedItem { OrderId = item.OrderId, ComponentId = item.ComponentId, Error = "Not assigned to this order's warehouse." });
                continue;
            }

            if (order.Status != OrderStatus.Assembly)
            {
                foreach (var item in orderGroup)
                    failedItems.Add(new BatchFulfillFailedItem { OrderId = item.OrderId, ComponentId = item.ComponentId, Error = "Order is not in Assembly status." });
                continue;
            }

            var attemptedTaskIds = new HashSet<Guid>();

            foreach (var item in orderGroup)
            {
                try
                {
                    var component = await db.AssemblyTaskBoxComponents
                        .Include(c => c.CatalogItem)
                        .Include(c => c.Fulfillments)
                        .FirstOrDefaultAsync(c => c.Id == item.ComponentId && c.AssemblyTaskBoxId == item.TaskBoxId, ct);

                    if (component is null)
                    {
                        failedItems.Add(new BatchFulfillFailedItem { OrderId = item.OrderId, ComponentId = item.ComponentId, Error = "Component not found." });
                        continue;
                    }

                    await orders.AddFulfillmentAsync(component, item.Fulfillment, ct);
                    attemptedTaskIds.Add(item.TaskId);
                }
                catch (ValidationException ex)
                {
                    failedItems.Add(new BatchFulfillFailedItem { OrderId = item.OrderId, ComponentId = item.ComponentId, Error = ex.Message });
                }
                catch (InsufficientInventoryException ex)
                {
                    failedItems.Add(new BatchFulfillFailedItem { OrderId = item.OrderId, ComponentId = item.ComponentId, Error = $"Insufficient inventory at node '{ex.NodeId}'." });
                }
                catch (UnitInventoryItemNotFoundException)
                {
                    failedItems.Add(new BatchFulfillFailedItem { OrderId = item.OrderId, ComponentId = item.ComponentId, Error = "Unit inventory item not found." });
                }
                catch (InventoryItemNodeMismatchException)
                {
                    failedItems.Add(new BatchFulfillFailedItem { OrderId = item.OrderId, ComponentId = item.ComponentId, Error = "Item is not at the expected storage node." });
                }
                catch (AssemblyComponentAlreadyFulfilledException)
                {
                    failedItems.Add(new BatchFulfillFailedItem { OrderId = item.OrderId, ComponentId = item.ComponentId, Error = "Component is already fully fulfilled." });
                }
            }

            // Attempt to auto-complete each processed task
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

        return Ok(new BatchFulfillResponse
        {
            CompletedTaskIds = completedTaskIds,
            FailedItems      = failedItems,
        });
    }
}
