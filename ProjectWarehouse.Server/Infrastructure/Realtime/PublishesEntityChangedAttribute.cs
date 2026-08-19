using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using ProjectWarehouse.Server.Domain;

namespace ProjectWarehouse.Server.Infrastructure.Realtime;

/// <summary>
/// Publishes <see cref="EntityChangedPayload"/> after any successful mutating action that addresses one
/// object by a route value. Entities with a changelog service get the event from there; orders have none,
/// and twenty call sites in the controller would be twenty chances to forget one.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class PublishesEntityChangedAttribute(AppEntityType entityType, string routeKey = "id")
    : Attribute, IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var executed = await next();

        if (executed.Exception is not null && !executed.ExceptionHandled) return;
        if (HttpMethods.IsGet(context.HttpContext.Request.Method)) return;
        if (executed.Result is IStatusCodeActionResult { StatusCode: { } status } && status is < 200 or >= 300) return;
        if (!context.RouteData.Values.TryGetValue(routeKey, out var raw) || !Guid.TryParse(raw?.ToString(), out var id))
            return;

        var notifier = context.HttpContext.RequestServices.GetRequiredService<IRealtimeNotifier>();
        await notifier.PublishEntityChangedAsync(entityType, id, context.HttpContext.User);
    }
}
