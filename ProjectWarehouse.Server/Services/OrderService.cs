using Microsoft.EntityFrameworkCore;
using ProjectWarehouse.Server.Data;
using ProjectWarehouse.Server.Domain;
using ProjectWarehouse.Server.Infrastructure;
using ProjectWarehouse.Server.Models.Orders;
using ProjectWarehouse.Server.Models.Receipts;

namespace ProjectWarehouse.Server.Services;

public class OrderService(ApplicationDbContext db, IInventoryService inventory) : IOrderService
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

    public async Task<IReadOnlyList<OrderBox>> GetMoveTargetsAsync(OrderBoxComponent component, CancellationToken ct = default)
    {
        return await db.OrderBoxes
            .Where(b => b.OrderId == component.OrderBox.OrderId && b.Id != component.OrderBoxId)
            .ToListAsync(ct);
    }

    public async Task MoveBoxComponentAsync(
        OrderBoxComponent component, MoveOrderBoxComponentRequest request, CancellationToken ct = default)
    {
        var hasTarget = request.TargetBoxId.HasValue;
        var hasNew    = !string.IsNullOrEmpty(request.NewBoxLabel);

        if (hasTarget == hasNew)
            throw new ValidationException("root", ErrorCode.ValidationError,
                "Exactly one of TargetBoxId or NewBoxLabel must be provided.");

        if (request.Quantity <= 0)
            throw new ValidationException("quantity", ErrorCode.OutOfRange, "Quantity must be greater than zero.");

        if (request.Quantity > component.Quantity)
            throw new ValidationException("quantity", ErrorCode.OutOfRange,
                $"Cannot move {request.Quantity}: component only has {component.Quantity}.");

        OrderBox targetBox;

        if (hasNew)
        {
            targetBox = new OrderBox
            {
                Id      = Guid.NewGuid(),
                OrderId = component.OrderBox.OrderId,
                Label   = request.NewBoxLabel,
            };
            db.OrderBoxes.Add(targetBox);
        }
        else
        {
            targetBox = await db.OrderBoxes
                .Include(b => b.Components)
                .FirstOrDefaultAsync(b => b.Id == request.TargetBoxId!.Value, ct)
                ?? throw new ValidationException("targetBoxId", ErrorCode.OrderBoxNotFound, "Target box not found.");

            if (targetBox.OrderId != component.OrderBox.OrderId)
                throw new ValidationException("targetBoxId", ErrorCode.OrderBoxNotFound,
                    "Target box does not belong to the same order.");
        }

        // Merge into existing component in target box or create new
        var targetComp = targetBox.Components.FirstOrDefault(c => c.CatalogItemId == component.CatalogItemId);
        if (targetComp is not null)
        {
            targetComp.Quantity += request.Quantity;
        }
        else
        {
            db.OrderBoxComponents.Add(new OrderBoxComponent
            {
                Id            = Guid.NewGuid(),
                OrderBoxId    = targetBox.Id,
                CatalogItemId = component.CatalogItemId,
                Quantity      = request.Quantity,
            });
        }

        component.Quantity -= request.Quantity;
        if (component.Quantity == 0)
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

        db.AssemblyTasks.Remove(task);
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

    public async Task UpdateTaskBoxComponentAsync(
        AssemblyTaskBoxComponent component, int quantity, CancellationToken ct = default)
    {
        component.Quantity = quantity;
        await db.SaveChangesAsync(ct);
    }

    // ── Fulfillments ──────────────────────────────────────────────────────────

    public async Task<AssemblyFulfillment> AddFulfillmentAsync(
        AssemblyTaskBoxComponent component,
        AddFulfillmentRequest request,
        CancellationToken ct = default)
    {
        var isBundleMode1 = request.BundleComponents is { Count: > 0 };
        var isUnit        = request.UnitInventoryItemId.HasValue;
        var isBundle      = request.AssembledBundleInventoryItemId.HasValue;
        var isStandard    = !isBundleMode1 && !isUnit && !isBundle && request.Quantity > 0;

        var setCount = (isBundleMode1 ? 1 : 0) + (isUnit ? 1 : 0) + (isBundle ? 1 : 0) + (isStandard ? 1 : 0);
        if (setCount != 1)
            throw new ValidationException("root", ErrorCode.AssemblyFulfillmentInvalidType,
                "Exactly one fulfillment type must be specified: Standard (sourceNodeId+quantity), Unit (unitInventoryItemId), AssembledBundle (assembledBundleInventoryItemId), or Bundle mode 1 (bundleComponents).");

        var fulfillment = new AssemblyFulfillment
        {
            Id                  = Guid.NewGuid(),
            TaskBoxComponentId  = component.Id,
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
                component.CatalogItemId,
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

            fulfillment.SourceNodeId         = request.SourceNodeId;
            fulfillment.UnitInventoryItemId  = request.UnitInventoryItemId;
            fulfillment.UnitInventoryNumber  = unitItem.InventoryNumber;

            db.AssemblyFulfillments.Add(fulfillment);

            await using var tx = await db.Database.BeginTransactionAsync(ct);

            await db.SaveChangesAsync(ct);

            await inventory.RemoveUnitItemAsync(
                request.UnitInventoryItemId!.Value,
                request.SourceNodeId.Value,
                InventoryActions.RemoveUnitItem,
                ct);

            await tx.CommitAsync(ct);
        }
        else if (isBundle)
        {
            if (!request.SourceNodeId.HasValue)
                throw new ValidationException("sourceNodeId", ErrorCode.Required, "SourceNodeId is required for AssembledBundle fulfillment.");

            var bundleItem = await db.InventoryItems.OfType<AssembledBundleInventoryItem>()
                .Include(b => b.Components).ThenInclude(c => c.UnitInventoryItem)
                .FirstOrDefaultAsync(b => b.Id == request.AssembledBundleInventoryItemId, ct)
                ?? throw new ValidationException("assembledBundleInventoryItemId", ErrorCode.AssembledBundleItemNotFound,
                    "Assembled bundle inventory item not found.");

            // Snapshot components before removal for restoration
            fulfillment.SourceNodeId                      = request.SourceNodeId;
            fulfillment.AssembledBundleInventoryItemId    = request.AssembledBundleInventoryItemId;

            foreach (var comp in bundleItem.Components)
            {
                fulfillment.AssembledBundleComponentSnapshots.Add(new AssemblyFulfillmentAssembledBundleComponentSnapshot
                {
                    Id                  = Guid.NewGuid(),
                    FulfillmentId       = fulfillment.Id,
                    UnitInventoryItemId = comp.UnitInventoryItemId,
                    CatalogItemId       = comp.CatalogItemId ?? comp.UnitInventoryItem?.CatalogItemId,
                    Quantity            = comp.Quantity,
                });
            }

            db.AssemblyFulfillments.Add(fulfillment);

            await using var tx = await db.Database.BeginTransactionAsync(ct);

            await db.SaveChangesAsync(ct);

            await inventory.RemoveAssembledBundleAsync(
                request.AssembledBundleInventoryItemId!.Value,
                request.SourceNodeId.Value,
                InventoryActions.RemoveAssembledBundle,
                ct);

            await tx.CommitAsync(ct);
        }
        else // Bundle mode 1
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
                    await inventory.RemoveUnitItemAsync(
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
        // Determine type and restore inventory
        if (fulfillment.BundleComponents.Count > 0)
        {
            // Bundle mode 1 — restore each component
            foreach (var comp in fulfillment.BundleComponents)
            {
                if (!string.IsNullOrEmpty(comp.UnitInventoryNumber))
                {
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
        else if (!string.IsNullOrEmpty(fulfillment.UnitInventoryNumber))
        {
            // Unit — recreate the unit item
            await inventory.PlaceUnitItemToNodeAsync(
                fulfillment.SourceNodeId!.Value,
                fulfillment.TaskBoxComponent.CatalogItemId,
                fulfillment.UnitInventoryNumber,
                InventoryActions.AddUnitItem,
                ct);
        }
        else if (fulfillment.AssembledBundleComponentSnapshots.Count > 0)
        {
            // AssembledBundle (mode 1 or 2) — recreate from snapshot
            var snapshotComponents = fulfillment.AssembledBundleComponentSnapshots
                .Select(s => new AssembledBundlePlacementComponentRequest
                {
                    CatalogItemId       = s.CatalogItemId
                        ?? throw new InvalidOperationException("Assembled bundle snapshot is missing CatalogItemId."),
                    UnitInventoryItemId = s.UnitInventoryItemId,
                    Quantity            = s.Quantity ?? 0,
                })
                .ToList();

            await inventory.AddAssembledBundleToNodeAsync(
                fulfillment.SourceNodeId!.Value,
                fulfillment.TaskBoxComponent.CatalogItemId,
                snapshotComponents,
                InventoryActions.AddAssembledBundle,
                ct);
        }
        else if (fulfillment.Quantity > 0 && fulfillment.SourceNodeId.HasValue)
        {
            // Standard
            await inventory.AddStandardItemsToNodeAsync(
                fulfillment.SourceNodeId.Value,
                fulfillment.TaskBoxComponent.CatalogItemId,
                fulfillment.Quantity,
                InventoryActions.AddStandardItems,
                ct);
        }

        db.AssemblyFulfillments.Remove(fulfillment);
        await db.SaveChangesAsync(ct);
    }

    // ── Private helpers ───────────────────────────────────────────────────────

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
