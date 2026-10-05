using Microsoft.AspNetCore.Mvc.Filters;
using ProjectWarehouse.Server.Services;

namespace ProjectWarehouse.Server.Infrastructure.Concurrency;

/// <summary>
/// <see cref="TransactionalAttribute" /> that also row-locks the entity named by a route value before the
/// action loads it, so every check the action makes runs against state no other writer can change until
/// the commit. Child routes lock their aggregate root — <c>/orders/{id}/boxes/{boxId}</c> locks the order.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public class LocksEntityAttribute<TEntity>(string routeKey = "id") : TransactionalAttribute where TEntity : class
{
    /// <summary>Set on the endpoint that deletes the entity itself; see <see cref="IEntityLockService.LockAsync{TEntity}" />.</summary>
    public bool ForDelete { get; init; }

    protected override Task AcquireAsync(ActionExecutingContext context, IEntityLockService locks, CancellationToken ct)
    {
        if (!context.RouteData.Values.TryGetValue(routeKey, out var raw) || !Guid.TryParse(raw?.ToString(), out var id))
            throw new InvalidOperationException($"Route value '{routeKey}' is missing or not a Guid.");

        return locks.LockAsync<TEntity>(id, ct, forDelete: ForDelete);
    }
}
