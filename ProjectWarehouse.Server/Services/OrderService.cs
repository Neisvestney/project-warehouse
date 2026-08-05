using Microsoft.EntityFrameworkCore;
using ProjectWarehouse.Server.Data;
using ProjectWarehouse.Server.Domain;
using ProjectWarehouse.Server.Infrastructure;
using ProjectWarehouse.Server.Models.Orders;
using ProjectWarehouse.Server.Models.Receipts;

namespace ProjectWarehouse.Server.Services;

public class OrderService(ApplicationDbContext db, IInventoryService inventory, ICatalogService catalog) : IOrderService
{
    // ── Order lifecycle ───────────────────────────────────────────────────────

    public async Task<Order> CreateDirectOrderAsync(
        CreateDirectOrderRequest request,
        Guid? createdById,
        CancellationToken ct = default)
    {
        var order = new Order
        {
            Id               = Guid.NewGuid(),
            Type             = OrderType.Direct,
            Status           = OrderStatus.Draft,
            WarehouseId      = request.WarehouseId,
            Notes            = request.Notes,
            PlannedShipmentAt = request.PlannedShipmentAt,
            CreatedById      = createdById,
            CreatedAt        = DateTime.UtcNow,
        };

        db.Orders.Add(order);
        await db.SaveChangesAsync(ct);

        return order;
    }

    public async Task UpdateOrderAsync(Order order, UpdateOrderRequest request, CancellationToken ct = default)
    {
        order.Notes            = request.Notes;
        order.PlannedShipmentAt = request.PlannedShipmentAt;
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteOrderAsync(Order order, CancellationToken ct = default)
    {
        if (order.Status != OrderStatus.Draft)
            throw new ValidationException("root", ErrorCode.OrderNotDraft, "Only Draft orders can be deleted.");

        db.Orders.Remove(order);
        await db.SaveChangesAsync(ct);
    }

    public async Task TransitionOrderStatusAsync(Order order, OrderStatus targetStatus, CancellationToken ct = default)
    {
        ValidateOrderTransition(order, targetStatus);

        if (order.Status == OrderStatus.Assembly && targetStatus == OrderStatus.Confirmed)
        {
            var tasks = await db.AssemblyTasks
                .Where(t => t.OrderId == order.Id)
                .Include(t => t.Boxes).ThenInclude(b => b.Components).ThenInclude(c => c.CatalogItem)
                .Include(t => t.Boxes).ThenInclude(b => b.Components).ThenInclude(c => c.Fulfillments)
                    .ThenInclude(f => f.BundleComponents)
                .ToListAsync(ct);

            foreach (var task in tasks)
                await RestoreAndDeleteTaskAsync(task, ct);
        }

        order.Status = targetStatus;
        await db.SaveChangesAsync(ct);
    }

    public async Task SelfAssignOrderAsync(Order order, Guid userId, CancellationToken ct = default)
    {
        if (order.Status != OrderStatus.Confirmed)
            throw new ValidationException("root", ErrorCode.OrderNotConfirmed,
                "Only Confirmed orders can be self-assigned.");

        // Load boxes with components
        var boxes = await db.OrderBoxes
            .Include(b => b.Components)
            .Where(b => b.OrderId == order.Id)
            .ToListAsync(ct);

        order.Status = OrderStatus.Assembly;

        var task = new AssemblyTask
        {
            Id           = Guid.NewGuid(),
            OrderId      = order.Id,
            AssignedToId = userId,
            Status       = AssemblyTaskStatus.Pending,
        };

        foreach (var box in boxes)
        {
            var taskBox = new AssemblyTaskBox
            {
                Id              = Guid.NewGuid(),
                AssemblyTaskId  = task.Id,
                OrderBoxId      = box.Id,
            };

            foreach (var comp in box.Components)
            {
                taskBox.Components.Add(new AssemblyTaskBoxComponent
                {
                    Id                = Guid.NewGuid(),
                    AssemblyTaskBoxId = taskBox.Id,
                    CatalogItemId     = comp.CatalogItemId,
                    Quantity          = comp.Quantity,
                });
            }

            task.Boxes.Add(taskBox);
        }

        db.AssemblyTasks.Add(task);
        await db.SaveChangesAsync(ct);
    }

    // ── Box management ────────────────────────────────────────────────────────

    public async Task<OrderBox> AddBoxAsync(Order order, CreateOrderBoxRequest request, CancellationToken ct = default)
    {
        if (order.Status is not (OrderStatus.Draft or OrderStatus.Confirmed or OrderStatus.Assembly))
            throw new ValidationException("root", ErrorCode.OrderInvalidStatusTransition,
                "Boxes can only be added in Draft, Confirmed, or Assembly status.");

        var box = new OrderBox
        {
            Id      = Guid.NewGuid(),
            OrderId = order.Id,
            Label   = request.Label,
        };

        db.OrderBoxes.Add(box);
        await db.SaveChangesAsync(ct);

        return box;
    }

    public async Task UpdateBoxAsync(OrderBox box, UpdateOrderBoxRequest request, CancellationToken ct = default)
    {
        box.Label = request.Label;
        await db.SaveChangesAsync(ct);
    }

    public async Task RemoveBoxAsync(OrderBox box, CancellationToken ct = default)
    {
        if (box.Components.Any())
            throw new ValidationException("root", ErrorCode.ValidationError, "Cannot remove a non-empty box.");

        db.OrderBoxes.Remove(box);
        await db.SaveChangesAsync(ct);
    }

    public async Task<OrderBoxComponent> UpsertBoxComponentAsync(
        OrderBox box, Guid catalogItemId, int quantity, CancellationToken ct = default)
    {
        var existing = box.Components.FirstOrDefault(c => c.CatalogItemId == catalogItemId);
        if (existing is not null)
        {
            existing.Quantity = quantity;
            await db.SaveChangesAsync(ct);
            return existing;
        }

        var component = new OrderBoxComponent
        {
            Id            = Guid.NewGuid(),
            OrderBoxId    = box.Id,
            CatalogItemId = catalogItemId,
            Quantity      = quantity,
        };

        db.OrderBoxComponents.Add(component);
        await db.SaveChangesAsync(ct);

        return component;
    }

    public async Task RemoveBoxComponentAsync(OrderBoxComponent component, CancellationToken ct = default)
    {
        db.OrderBoxComponents.Remove(component);
        await db.SaveChangesAsync(ct);
    }

    // ── Assembly task management ──────────────────────────────────────────────

    public async Task<AssemblyTask> CreateAssemblyTaskAsync(
        Order order, CreateAssemblyTaskRequest request, CancellationToken ct = default)
    {
        if (order.Status != OrderStatus.Assembly)
            throw new ValidationException("root", ErrorCode.OrderNotAssembly,
                "Assembly tasks can only be created when the order is in Assembly status.");

        var task = new AssemblyTask
        {
            Id           = Guid.NewGuid(),
            OrderId      = order.Id,
            AssignedToId = request.AssignedToId,
            Status       = AssemblyTaskStatus.Pending,
        };

        for (var i = 0; i < request.Boxes.Count; i++)
        {
            var boxReq = request.Boxes[i];
            var prefix = $"boxes[{i}]";

            var orderBox = await db.OrderBoxes.FirstOrDefaultAsync(b => b.Id == boxReq.OrderBoxId && b.OrderId == order.Id, ct)
                ?? throw new ValidationException($"{prefix}.orderBoxId", ErrorCode.OrderBoxNotFound,
                    $"Order box '{boxReq.OrderBoxId}' not found in this order.");

            var taskBox = new AssemblyTaskBox
            {
                Id             = Guid.NewGuid(),
                AssemblyTaskId = task.Id,
                OrderBoxId     = orderBox.Id,
            };

            for (var j = 0; j < boxReq.Components.Count; j++)
            {
                var compReq = boxReq.Components[j];

                var available = await GetAvailableQuantityAsync(orderBox.Id, compReq.CatalogItemId, excludeTaskId: null, ct);
                if (available is null)
                    throw new ValidationException($"{prefix}.components[{j}].catalogItemId", ErrorCode.OrderBoxComponentNotFound,
                        $"Catalog item '{compReq.CatalogItemId}' not found in box '{orderBox.Id}'.");
                if (compReq.Quantity > available)
                    throw new ValidationException($"{prefix}.components[{j}].quantity", ErrorCode.AssemblyTaskQuantityExceedsAvailable,
                        $"Requested quantity {compReq.Quantity} exceeds available quantity {available} for this catalog item in this box.");

                taskBox.Components.Add(new AssemblyTaskBoxComponent
                {
                    Id                = Guid.NewGuid(),
                    AssemblyTaskBoxId = taskBox.Id,
                    CatalogItemId     = compReq.CatalogItemId,
                    Quantity          = compReq.Quantity,
                });
            }

            task.Boxes.Add(taskBox);
        }

        db.AssemblyTasks.Add(task);
        await db.SaveChangesAsync(ct);

        return task;
    }

    public async Task UpdateAssemblyTaskAsync(AssemblyTask task, UpdateAssemblyTaskRequest request, CancellationToken ct = default)
    {
        if (task.Status == AssemblyTaskStatus.Done)
            throw new ValidationException("root", ErrorCode.AssemblyTaskAlreadyDone,
                "Cannot update a completed assembly task.");

        task.AssignedToId = request.AssignedToId;
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAssemblyTaskAsync(AssemblyTask task, CancellationToken ct = default)
    {
        var order = await db.Orders.FindAsync([task.OrderId], ct)
            ?? throw new ValidationException("root", ErrorCode.OrderNotFound, "Order not found.");

        if (order.Status != OrderStatus.Assembly)
            throw new ValidationException("root", ErrorCode.AssemblyTaskNotDeletable,
                "Assembly tasks can only be deleted when the order is in Assembly status.");

        var fullTask = await db.AssemblyTasks
            .Include(t => t.Boxes).ThenInclude(b => b.Components).ThenInclude(c => c.CatalogItem)
            .Include(t => t.Boxes).ThenInclude(b => b.Components).ThenInclude(c => c.Fulfillments)
                .ThenInclude(f => f.BundleComponents)
            .FirstAsync(t => t.Id == task.Id, ct);

        await RestoreAndDeleteTaskAsync(fullTask, ct);
        await db.SaveChangesAsync(ct);
    }

    public async Task TransitionTaskStatusAsync(
        AssemblyTask task, AssemblyTaskStatus targetStatus, Order order, CancellationToken ct = default)
    {
        var valid = (task.Status, targetStatus) switch
        {
            (AssemblyTaskStatus.Pending,    AssemblyTaskStatus.InProgress) => true,
            (AssemblyTaskStatus.InProgress, AssemblyTaskStatus.Done)       => true,
            (AssemblyTaskStatus.InProgress, AssemblyTaskStatus.Pending)    => true,
            _                                                              => false,
        };

        if (!valid)
            throw new ValidationException("targetStatus", ErrorCode.OrderInvalidStatusTransition,
                $"Cannot transition task from {task.Status} to {targetStatus}.");

        task.Status = targetStatus;

        // Auto-transition order to Assembled when all tasks are Done
        if (targetStatus == AssemblyTaskStatus.Done)
        {
            var allDone = await db.AssemblyTasks
                .Where(t => t.OrderId == order.Id && t.Id != task.Id)
                .AllAsync(t => t.Status == AssemblyTaskStatus.Done, ct);

            if (allDone && order.Status == OrderStatus.Assembly)
                order.Status = OrderStatus.Assembled;
        }

        await db.SaveChangesAsync(ct);
    }

    public async Task<bool> IsTaskFullyFulfilledAsync(Guid taskId, CancellationToken ct = default)
    {
        var components = await db.AssemblyTaskBoxComponents
            .Where(c => c.AssemblyTaskBox.AssemblyTaskId == taskId)
            .Include(c => c.Fulfillments)
                .ThenInclude(f => f.BundleComponents)
            .ToListAsync(ct);

        return components.Count > 0
            && components.All(c => CountFulfilledQty(c.Fulfillments) >= c.Quantity);
    }

    public async Task UpdateTaskBoxComponentAsync(
        AssemblyTaskBoxComponent component, int quantity, CancellationToken ct = default)
    {
        var available = await GetAvailableQuantityAsync(
            component.AssemblyTaskBox.OrderBoxId, component.CatalogItemId,
            excludeTaskId: component.AssemblyTaskBox.AssemblyTaskId, ct);

        if (quantity > available)
            throw new ValidationException("quantity", ErrorCode.AssemblyTaskQuantityExceedsAvailable,
                $"Requested quantity {quantity} exceeds available quantity {available} for this catalog item in this box.");

        component.Quantity = quantity;
        await db.SaveChangesAsync(ct);
    }

    // ── Task-scoped box/component moves (Assembly, assembler-only) ─────────────

    /// <summary>Returns all order boxes except the one the given task box component currently sits in.</summary>
    public async Task<IReadOnlyList<OrderBox>> GetTaskMoveTargetsAsync(
        AssemblyTaskBoxComponent component, CancellationToken ct = default)
    {
        return await db.OrderBoxes
            .Where(b => b.OrderId == component.AssemblyTaskBox.OrderBox.OrderId
                     && b.Id != component.AssemblyTaskBox.OrderBoxId)
            .ToListAsync(ct);
    }

    /// <summary>
    /// Moves part or all of a task's own allocation of a component to another box.
    /// Updates both the assembly task's own component split (AssemblyTaskBoxComponent) and the
    /// order's overall composition (OrderBoxComponent) — the move only affects this task's allocation,
    /// other tasks referencing the same OrderBox/CatalogItem are untouched.
    /// </summary>
    public async Task MoveTaskBoxComponentAsync(
        AssemblyTaskBoxComponent component, MoveTaskBoxComponentRequest request, CancellationToken ct = default)
    {
        var hasTarget = request.TargetBoxId.HasValue;
        var hasNew    = !string.IsNullOrEmpty(request.NewBoxLabel);

        if (hasTarget == hasNew)
            throw new ValidationException("root", ErrorCode.ValidationError,
                "Exactly one of TargetBoxId or NewBoxLabel must be provided.");

        var fulfilledQty = CountFulfilledQty(component.Fulfillments);
        var movable = component.Quantity - fulfilledQty;

        if (request.Quantity <= 0 || request.Quantity > movable)
            throw new ValidationException("quantity", ErrorCode.OutOfRange,
                $"Cannot move {request.Quantity}: only {movable} unfulfilled unit(s) available in this task.");

        var sourceOrderBoxId = component.AssemblyTaskBox.OrderBoxId;
        var taskId            = component.AssemblyTaskBox.AssemblyTaskId;
        var catalogItemId     = component.CatalogItemId;
        var orderId           = component.AssemblyTaskBox.OrderBox.OrderId;

        // 1) Resolve/create the target OrderBox
        OrderBox targetOrderBox;
        if (hasNew)
        {
            targetOrderBox = new OrderBox { Id = Guid.NewGuid(), OrderId = orderId, Label = request.NewBoxLabel };
            db.OrderBoxes.Add(targetOrderBox);
        }
        else
        {
            targetOrderBox = await db.OrderBoxes
                .Include(b => b.Components)
                .FirstOrDefaultAsync(b => b.Id == request.TargetBoxId!.Value, ct)
                ?? throw new ValidationException("targetBoxId", ErrorCode.OrderBoxNotFound, "Target box not found.");

            if (targetOrderBox.OrderId != orderId)
                throw new ValidationException("targetBoxId", ErrorCode.OrderBoxNotFound,
                    "Target box does not belong to the same order.");

            if (targetOrderBox.Id == sourceOrderBoxId)
                throw new ValidationException("targetBoxId", ErrorCode.ValidationError,
                    "Target box must be different from the source box.");
        }

        // 2) Update the order's overall composition (OrderBoxComponent)
        var sourceOrderComp = await db.OrderBoxComponents
            .FirstOrDefaultAsync(c => c.OrderBoxId == sourceOrderBoxId && c.CatalogItemId == catalogItemId, ct)
            ?? throw new ValidationException("root", ErrorCode.OrderBoxComponentNotFound, "Source order box component not found.");

        sourceOrderComp.Quantity -= request.Quantity;
        if (sourceOrderComp.Quantity == 0)
            db.OrderBoxComponents.Remove(sourceOrderComp);

        var targetOrderComp = hasNew ? null : targetOrderBox.Components.FirstOrDefault(c => c.CatalogItemId == catalogItemId);
        if (targetOrderComp is not null)
        {
            targetOrderComp.Quantity += request.Quantity;
        }
        else
        {
            db.OrderBoxComponents.Add(new OrderBoxComponent
            {
                Id            = Guid.NewGuid(),
                OrderBoxId    = targetOrderBox.Id,
                CatalogItemId = catalogItemId,
                Quantity      = request.Quantity,
            });
        }

        // 3) Update this task's own allocation (AssemblyTaskBoxComponent)
        var targetTaskBox = await db.AssemblyTaskBoxes
            .Include(b => b.Components)
            .FirstOrDefaultAsync(b => b.AssemblyTaskId == taskId && b.OrderBoxId == targetOrderBox.Id, ct);

        if (targetTaskBox is null)
        {
            targetTaskBox = new AssemblyTaskBox { Id = Guid.NewGuid(), AssemblyTaskId = taskId, OrderBoxId = targetOrderBox.Id };
            db.AssemblyTaskBoxes.Add(targetTaskBox);
        }

        var targetTaskComp = targetTaskBox.Components.FirstOrDefault(c => c.CatalogItemId == catalogItemId);
        if (targetTaskComp is not null)
        {
            targetTaskComp.Quantity += request.Quantity;
        }
        else
        {
            db.AssemblyTaskBoxComponents.Add(new AssemblyTaskBoxComponent
            {
                Id                = Guid.NewGuid(),
                AssemblyTaskBoxId = targetTaskBox.Id,
                CatalogItemId     = catalogItemId,
                Quantity          = request.Quantity,
            });
        }

        var sourceTaskBox = component.AssemblyTaskBox;
        component.Quantity -= request.Quantity;
        if (component.Quantity == 0)
        {
            db.AssemblyTaskBoxComponents.Remove(component);
            if (sourceTaskBox.Components.All(c => c.Id == component.Id))
                db.AssemblyTaskBoxes.Remove(sourceTaskBox);
        }

        await db.SaveChangesAsync(ct);
    }

    // ── Fulfillments ──────────────────────────────────────────────────────────

    public async Task<AssemblyFulfillment> AddFulfillmentAsync(
        AssemblyTaskBoxComponent component,
        AddFulfillmentRequest request,
        Guid? createdById,
        CancellationToken ct = default)
    {
        // Re-check against freshly-loaded fulfillment state (not the possibly-stale
        // navigation on the passed-in component) to guard against duplicate/concurrent
        // calls re-fulfilling a component that's already fully satisfied.
        var existingFulfillments = await db.AssemblyFulfillments
            .Where(f => f.TaskBoxComponentId == component.Id)
            .Select(f => new { f.Quantity, f.UnitInventoryItemId, HasBundleComponents = f.BundleComponents.Count > 0 })
            .ToListAsync(ct);

        var alreadyFulfilled = existingFulfillments.Sum(f =>
            f.UnitInventoryItemId.HasValue || f.HasBundleComponents ? 1 : f.Quantity);

        if (alreadyFulfilled >= component.Quantity)
            throw new AssemblyComponentAlreadyFulfilledException(component.Id);

        var isBundleMode1 = request.BundleComponents is { Count: > 0 };
        var isUnit        = request.UnitInventoryItemId.HasValue;
        var isStandard    = !isBundleMode1 && !isUnit && request.Quantity > 0;

        var setCount = (isBundleMode1 ? 1 : 0) + (isUnit ? 1 : 0) + (isStandard ? 1 : 0);
        if (setCount != 1)
            throw new ValidationException("root", ErrorCode.AssemblyFulfillmentInvalidType,
                "Exactly one fulfillment type must be specified: Standard (sourceNodeId+quantity), Unit (unitInventoryItemId), or Bundle (bundleComponents).");

        var isVariation = component.CatalogItem.Type == CatalogItemType.Variation;

        // For a Variation the client picks a concrete member; everything else resolves to itself.
        // A plain Bundle deducts per leaf, so it needs no resolved item at all.
        Guid? resolvedCatalogItemId = null;
        if (!isVariation)
        {
            resolvedCatalogItemId = isBundleMode1 ? null : component.CatalogItemId;
        }
        else if (!isUnit)
        {
            if (!request.ResolvedCatalogItemId.HasValue)
                throw new ValidationException("resolvedCatalogItemId", ErrorCode.Required,
                    "ResolvedCatalogItemId is required when fulfilling a Variation component.");

            var resolvedType = await db.CatalogItems
                .Where(c => c.Id == request.ResolvedCatalogItemId.Value)
                .Select(c => (CatalogItemType?)c.Type)
                .FirstOrDefaultAsync(ct)
                ?? throw new ValidationException("resolvedCatalogItemId", ErrorCode.CatalogItemNotFound,
                    "Resolved catalog item not found.");

            var expectedType = isBundleMode1 ? CatalogItemType.Bundle : CatalogItemType.Standard;
            if (resolvedType != expectedType)
                throw new ValidationException("resolvedCatalogItemId", ErrorCode.AssemblyFulfillmentInvalidType,
                    $"Resolved catalog item must be of type {expectedType} for this fulfillment scenario.");

            if (!await catalog.IsVariationMemberAsync(component.CatalogItemId, request.ResolvedCatalogItemId.Value, ct))
                throw new ValidationException("resolvedCatalogItemId", ErrorCode.CatalogItemNotVariationMember,
                    "Resolved catalog item is not a member of this variation.");

            // Kept even for Bundle (where deduction is per leaf) so the UI can still show the choice.
            resolvedCatalogItemId = request.ResolvedCatalogItemId;
        }

        var fulfillment = new AssemblyFulfillment
        {
            Id                    = Guid.NewGuid(),
            TaskBoxComponentId    = component.Id,
            ResolvedCatalogItemId = resolvedCatalogItemId,
            CreatedById           = createdById,
        };

        if (isStandard)
        {
            if (!request.SourceNodeId.HasValue)
                throw new ValidationException("sourceNodeId", ErrorCode.Required, "SourceNodeId is required for Standard fulfillment.");

            fulfillment.SourceNodeId  = request.SourceNodeId;
            fulfillment.Quantity      = request.Quantity;

            db.AssemblyFulfillments.Add(fulfillment);

            await using var tx = await db.Database.BeginTransactionAsync(ct);

            await db.SaveChangesAsync(ct);

            await inventory.RemoveStandardItemsFromNodeAsync(
                request.SourceNodeId.Value,
                fulfillment.ResolvedCatalogItemId!.Value,
                request.Quantity,
                InventoryActions.RemoveStandardItems,
                ct);

            await tx.CommitAsync(ct);
        }
        else if (isUnit)
        {
            if (!request.SourceNodeId.HasValue)
                throw new ValidationException("sourceNodeId", ErrorCode.Required, "SourceNodeId is required for Unit fulfillment.");

            var unitItem = await db.InventoryItems.OfType<UnitInventoryItem>()
                .FirstOrDefaultAsync(u => u.Id == request.UnitInventoryItemId, ct)
                ?? throw new ValidationException("unitInventoryItemId", ErrorCode.UnitInventoryItemNotFound, "Unit inventory item not found.");

            if (isVariation &&
                !await catalog.IsVariationMemberAsync(component.CatalogItemId, unitItem.CatalogItemId, ct))
                throw new ValidationException("unitInventoryItemId", ErrorCode.CatalogItemNotVariationMember,
                    "The unit inventory item's catalog entry is not a member of this variation.");

            fulfillment.SourceNodeId         = request.SourceNodeId;
            fulfillment.UnitInventoryItemId  = request.UnitInventoryItemId;
            fulfillment.UnitInventoryNumber  = unitItem.InventoryNumber;

            // The item itself is authoritative about which catalog entry was picked.
            fulfillment.ResolvedCatalogItemId = unitItem.CatalogItemId;

            db.AssemblyFulfillments.Add(fulfillment);

            await using var tx = await db.Database.BeginTransactionAsync(ct);

            await db.SaveChangesAsync(ct);

            await inventory.DetachUnitItemAsync(
                request.UnitInventoryItemId!.Value,
                request.SourceNodeId.Value,
                InventoryActions.RemoveUnitItem,
                ct);

            await tx.CommitAsync(ct);
        }
        else // Bundle
        {
            fulfillment.SourceNodeId = null;

            db.AssemblyFulfillments.Add(fulfillment);

            foreach (var compReq in request.BundleComponents!)
            {
                if (compReq.UnitInventoryItemId.HasValue)
                {
                    var unitItem = await db.InventoryItems.OfType<UnitInventoryItem>()
                        .FirstOrDefaultAsync(u => u.Id == compReq.UnitInventoryItemId, ct)
                        ?? throw new ValidationException("bundleComponents.unitInventoryItemId",
                            ErrorCode.UnitInventoryItemNotFound, "Unit inventory item not found.");

                    fulfillment.BundleComponents.Add(new AssemblyFulfillmentBundleComponent
                    {
                        Id                  = Guid.NewGuid(),
                        FulfillmentId       = fulfillment.Id,
                        CatalogItemId       = compReq.CatalogItemId,
                        SourceNodeId        = compReq.SourceNodeId,
                        UnitInventoryItemId = compReq.UnitInventoryItemId,
                        UnitInventoryNumber = unitItem.InventoryNumber,
                    });
                }
                else
                {
                    fulfillment.BundleComponents.Add(new AssemblyFulfillmentBundleComponent
                    {
                        Id            = Guid.NewGuid(),
                        FulfillmentId = fulfillment.Id,
                        CatalogItemId = compReq.CatalogItemId,
                        SourceNodeId  = compReq.SourceNodeId,
                        Quantity      = compReq.Quantity,
                    });
                }
            }

            await using var tx = await db.Database.BeginTransactionAsync(ct);

            await db.SaveChangesAsync(ct);

            // Deduct inventory for each bundle component; tx rolls back on any failure
            foreach (var bundleComp in fulfillment.BundleComponents)
            {
                if (bundleComp.UnitInventoryItemId.HasValue)
                {
                    await inventory.DetachUnitItemAsync(
                        bundleComp.UnitInventoryItemId.Value,
                        bundleComp.SourceNodeId,
                        InventoryActions.RemoveUnitItem,
                        ct);
                }
                else
                {
                    await inventory.RemoveStandardItemsFromNodeAsync(
                        bundleComp.SourceNodeId,
                        bundleComp.CatalogItemId,
                        bundleComp.Quantity,
                        InventoryActions.RemoveStandardItems,
                        ct);
                }
            }

            await tx.CommitAsync(ct);
        }

        return fulfillment;
    }

    public async Task RemoveFulfillmentAsync(AssemblyFulfillment fulfillment, CancellationToken ct = default)
    {
        await RestoreFulfillmentInventoryAsync(fulfillment, ct);
        db.AssemblyFulfillments.Remove(fulfillment);
        await db.SaveChangesAsync(ct);
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private async Task RestoreFulfillmentInventoryAsync(AssemblyFulfillment fulfillment, CancellationToken ct)
    {
        // Determine type and restore inventory
        if (fulfillment.BundleComponents.Count > 0)
        {
            // Bundle — restore each component
            foreach (var comp in fulfillment.BundleComponents)
            {
                if (comp.UnitInventoryItemId.HasValue)
                {
                    await inventory.ReattachUnitItemAsync(
                        comp.UnitInventoryItemId.Value,
                        comp.SourceNodeId,
                        InventoryActions.AddUnitItem,
                        ct);
                }
                else if (!string.IsNullOrEmpty(comp.UnitInventoryNumber))
                {
                    // Legacy row from before the detach/reattach refactor — the item was
                    // hard-deleted, so recreate it from the snapshot instead.
                    await inventory.PlaceUnitItemToNodeAsync(
                        comp.SourceNodeId,
                        comp.CatalogItemId,
                        comp.UnitInventoryNumber,
                        InventoryActions.AddUnitItem,
                        ct);
                }
                else
                {
                    await inventory.AddStandardItemsToNodeAsync(
                        comp.SourceNodeId,
                        comp.CatalogItemId,
                        comp.Quantity,
                        InventoryActions.AddStandardItems,
                        ct);
                }
            }
        }
        else if (fulfillment.UnitInventoryItemId.HasValue)
        {
            // Unit — reattach the same item to its original node
            await inventory.ReattachUnitItemAsync(
                fulfillment.UnitInventoryItemId.Value,
                fulfillment.SourceNodeId!.Value,
                InventoryActions.AddUnitItem,
                ct);
        }
        else if (!string.IsNullOrEmpty(fulfillment.UnitInventoryNumber))
        {
            // Legacy row from before the detach/reattach refactor — the item was
            // hard-deleted, so recreate it from the snapshot instead.
            await inventory.PlaceUnitItemToNodeAsync(
                fulfillment.SourceNodeId!.Value,
                RestoreTargetCatalogItemId(fulfillment),
                fulfillment.UnitInventoryNumber,
                InventoryActions.AddUnitItem,
                ct);
        }
        else if (fulfillment.Quantity > 0 && fulfillment.SourceNodeId.HasValue)
        {
            // Standard
            await inventory.AddStandardItemsToNodeAsync(
                fulfillment.SourceNodeId.Value,
                RestoreTargetCatalogItemId(fulfillment),
                fulfillment.Quantity,
                InventoryActions.AddStandardItems,
                ct);
        }
    }

    /// <summary>Pre-migration rows have no resolved item; they were deducted from the component's own item, so they go back the same way.</summary>
    private static Guid RestoreTargetCatalogItemId(AssemblyFulfillment fulfillment) =>
        fulfillment.ResolvedCatalogItemId ?? fulfillment.TaskBoxComponent.CatalogItemId;

    /// <summary>Restores inventory for every fulfillment under the task, then removes the task (cascades boxes/components/fulfillments). Does not call SaveChanges.</summary>
    private async Task RestoreAndDeleteTaskAsync(AssemblyTask task, CancellationToken ct)
    {
        foreach (var box in task.Boxes)
            foreach (var comp in box.Components)
                foreach (var fulfillment in comp.Fulfillments.ToList())
                    await RestoreFulfillmentInventoryAsync(fulfillment, ct);

        db.AssemblyTasks.Remove(task);
    }

    /// <summary>Same counting convention as the frontend's countFulfilledQty: a Unit/Bundle fulfillment always counts as 1, Standard counts by Quantity.</summary>
    private static int CountFulfilledQty(IEnumerable<AssemblyFulfillment> fulfillments)
    {
        var sum = 0;
        foreach (var f in fulfillments)
            sum += (f.UnitInventoryItemId.HasValue || f.BundleComponents.Count > 0) ? 1 : f.Quantity;
        return sum;
    }

    /// <summary>OrderBoxComponent.Quantity minus what's already allocated to OTHER assembly tasks for the same box+item. Null if the catalog item isn't in this box at all.</summary>
    private async Task<int?> GetAvailableQuantityAsync(
        Guid orderBoxId, Guid catalogItemId, Guid? excludeTaskId, CancellationToken ct)
    {
        var boxComponent = await db.OrderBoxComponents
            .FirstOrDefaultAsync(c => c.OrderBoxId == orderBoxId && c.CatalogItemId == catalogItemId, ct);
        if (boxComponent is null)
            return null;

        var allocatedElsewhere = await db.AssemblyTaskBoxComponents
            .Where(c => c.AssemblyTaskBox.OrderBoxId == orderBoxId
                     && c.CatalogItemId == catalogItemId
                     && (excludeTaskId == null || c.AssemblyTaskBox.AssemblyTaskId != excludeTaskId))
            .SumAsync(c => (int?)c.Quantity, ct) ?? 0;

        return boxComponent.Quantity - allocatedElsewhere;
    }

    private static void ValidateOrderTransition(Order order, OrderStatus target)
    {
        var valid = (order.Status, target) switch
        {
            (OrderStatus.Draft,     OrderStatus.Confirmed)  => true,
            (OrderStatus.Draft,     OrderStatus.Canceled)   => true,
            (OrderStatus.Confirmed, OrderStatus.Draft)      => true,
            (OrderStatus.Confirmed, OrderStatus.Assembly)   => true,
            (OrderStatus.Confirmed, OrderStatus.Canceled)   => true,
            (OrderStatus.Assembly,  OrderStatus.Confirmed)  => true,
            (OrderStatus.Assembly,  OrderStatus.Canceled)   => true,
            (OrderStatus.Assembled, OrderStatus.Shipped)    => true,
            _                                               => false,
        };

        if (!valid)
            throw new ValidationException("targetStatus", ErrorCode.OrderInvalidStatusTransition,
                $"Cannot transition order from {order.Status} to {target}.");

        // Rollback Assembly → Confirmed only if no tasks are Done
        if (order.Status == OrderStatus.Assembly && target == OrderStatus.Confirmed)
        {
            var hasDoneTask = order.AssemblyTasks.Any(t => t.Status == AssemblyTaskStatus.Done);
            if (hasDoneTask)
                throw new ValidationException("targetStatus", ErrorCode.OrderInvalidStatusTransition,
                    "Cannot roll back to Confirmed: one or more assembly tasks are already completed.");
        }

        // Cancel only if no Fulfillments exist
        if (target == OrderStatus.Canceled)
        {
            var hasFulfillments = order.AssemblyTasks
                .SelectMany(t => t.Boxes)
                .SelectMany(b => b.Components)
                .SelectMany(c => c.Fulfillments)
                .Any();

            if (hasFulfillments)
                throw new ValidationException("targetStatus", ErrorCode.OrderHasFulfillments,
                    "Cannot cancel: remove all fulfillments before canceling.");
        }
    }
}
