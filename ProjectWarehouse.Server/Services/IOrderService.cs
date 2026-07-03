using ProjectWarehouse.Server.Domain;
using ProjectWarehouse.Server.Models.Orders;

namespace ProjectWarehouse.Server.Services;

public interface IOrderService
{
    // ── Order lifecycle ───────────────────────────────────────────────────────

    Task<Order> CreateDirectOrderAsync(CreateDirectOrderRequest request, Guid? createdById, CancellationToken ct = default);
    Task UpdateOrderAsync(Order order, UpdateOrderRequest request, CancellationToken ct = default);
    Task DeleteOrderAsync(Order order, CancellationToken ct = default);

    /// <summary>Validates and executes a status transition on the order.</summary>
    Task TransitionOrderStatusAsync(Order order, OrderStatus targetStatus, CancellationToken ct = default);

    /// <summary>
    /// Worker self-assigns a Confirmed order: transitions to Assembly and creates a single task covering all boxes.
    /// Requires the order to be in Confirmed status and in an assigned warehouse.
    /// </summary>
    Task SelfAssignOrderAsync(Order order, Guid userId, CancellationToken ct = default);

    // ── Box management ────────────────────────────────────────────────────────

    Task<OrderBox> AddBoxAsync(Order order, CreateOrderBoxRequest request, CancellationToken ct = default);
    Task UpdateBoxAsync(OrderBox box, UpdateOrderBoxRequest request, CancellationToken ct = default);
    Task RemoveBoxAsync(OrderBox box, CancellationToken ct = default);

    Task<OrderBoxComponent> UpsertBoxComponentAsync(OrderBox box, Guid catalogItemId, int quantity, CancellationToken ct = default);
    Task RemoveBoxComponentAsync(OrderBoxComponent component, CancellationToken ct = default);

    /// <summary>Returns all boxes in the order except the one that contains the given component.</summary>
    Task<IReadOnlyList<OrderBox>> GetMoveTargetsAsync(OrderBoxComponent component, CancellationToken ct = default);

    /// <summary>
    /// Partially or fully moves a component to another box.
    /// Supports moving to an existing box (TargetBoxId) or creating a new box (NewBoxLabel).
    /// </summary>
    Task MoveBoxComponentAsync(OrderBoxComponent component, MoveOrderBoxComponentRequest request, CancellationToken ct = default);

    // ── Assembly task management ──────────────────────────────────────────────

    Task<AssemblyTask> CreateAssemblyTaskAsync(Order order, CreateAssemblyTaskRequest request, CancellationToken ct = default);
    Task UpdateAssemblyTaskAsync(AssemblyTask task, UpdateAssemblyTaskRequest request, CancellationToken ct = default);
    Task DeleteAssemblyTaskAsync(AssemblyTask task, CancellationToken ct = default);
    Task TransitionTaskStatusAsync(AssemblyTask task, AssemblyTaskStatus targetStatus, Order order, CancellationToken ct = default);
    Task UpdateTaskBoxComponentAsync(AssemblyTaskBoxComponent component, int quantity, CancellationToken ct = default);

    // ── Fulfillments ──────────────────────────────────────────────────────────

    Task<AssemblyFulfillment> AddFulfillmentAsync(AssemblyTaskBoxComponent component, AddFulfillmentRequest request, CancellationToken ct = default);
    Task RemoveFulfillmentAsync(AssemblyFulfillment fulfillment, CancellationToken ct = default);
}
