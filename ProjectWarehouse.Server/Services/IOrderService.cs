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

    // ── Assembly task management ──────────────────────────────────────────────

    Task<AssemblyTask> CreateAssemblyTaskAsync(Order order, CreateAssemblyTaskRequest request, CancellationToken ct = default);
    Task UpdateAssemblyTaskAsync(AssemblyTask task, UpdateAssemblyTaskRequest request, CancellationToken ct = default);
    Task DeleteAssemblyTaskAsync(AssemblyTask task, CancellationToken ct = default);
    Task TransitionTaskStatusAsync(AssemblyTask task, AssemblyTaskStatus targetStatus, Order order, CancellationToken ct = default);

    /// <summary>True when every component of every box of the task is fulfilled up to its required quantity.</summary>
    Task<bool> IsTaskFullyFulfilledAsync(Guid taskId, CancellationToken ct = default);
    Task UpdateTaskBoxComponentAsync(AssemblyTaskBoxComponent component, int quantity, CancellationToken ct = default);

    /// <summary>Returns all order boxes except the one the given task box component currently sits in.</summary>
    Task<IReadOnlyList<OrderBox>> GetTaskMoveTargetsAsync(AssemblyTaskBoxComponent component, CancellationToken ct = default);

    /// <summary>
    /// Moves part or all of a task's own allocation of a component to another box (existing or newly created).
    /// Updates both this task's own split and the order's overall composition; other tasks are untouched.
    /// </summary>
    Task MoveTaskBoxComponentAsync(AssemblyTaskBoxComponent component, MoveTaskBoxComponentRequest request, CancellationToken ct = default);

    // ── Fulfillments ──────────────────────────────────────────────────────────

    Task<AssemblyFulfillment> AddFulfillmentAsync(AssemblyTaskBoxComponent component, AddFulfillmentRequest request, Guid? createdById, CancellationToken ct = default);
    Task RemoveFulfillmentAsync(AssemblyFulfillment fulfillment, CancellationToken ct = default);
}
