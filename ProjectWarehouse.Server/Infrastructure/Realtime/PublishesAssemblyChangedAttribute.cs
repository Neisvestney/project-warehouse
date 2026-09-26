using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using ProjectWarehouse.Server.Services;

namespace ProjectWarehouse.Server.Infrastructure.Realtime;

/// <summary>
/// Publishes <see cref="AssemblyChangedPayload"/> after a successful mutating action on one order. The order's
/// worklist state is captured before the action runs, so a status change or a reassignment is reported as
/// <see cref="AssemblyChangeScope.List"/> to the old assignees as well as the new ones.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public class PublishesAssemblyChangedAttribute(AssemblyChangeScope scope) : Attribute, IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (HttpMethods.IsGet(context.HttpContext.Request.Method)
            || !TryGetRouteGuid(context, "id", out var orderId))
        {
            await next();
            return;
        }

        var services = context.HttpContext.RequestServices;
        var notifier = services.GetRequiredService<IAssemblyChangeNotifier>();
        var logger = services.GetRequiredService<ILogger<PublishesAssemblyChangedAttribute>>();
        var ct = context.HttpContext.RequestAborted;

        // The event is only a hint: failing to announce a change must neither block nor fail the mutation itself.
        IReadOnlyDictionary<Guid, AssemblyOrderState>? before = null;
        try
        {
            before = await notifier.CaptureAsync([orderId], ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not capture assembly state of order {OrderId}; no assemblyChanged will be published",
                orderId);
        }

        var executed = await next();

        if (before is null) return;
        if (executed.Exception is not null && !executed.ExceptionHandled) return;
        if (executed.Result is IStatusCodeActionResult { StatusCode: { } status } && status is < 200 or >= 300) return;

        Guid? taskId = TryGetRouteGuid(context, "taskId", out var parsed) ? parsed : null;
        try
        {
            await notifier.PublishAsync(before, scope, taskId, context.HttpContext, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not publish assemblyChanged for order {OrderId}", orderId);
        }
    }

    private static bool TryGetRouteGuid(ActionExecutingContext context, string key, out Guid value)
    {
        value = Guid.Empty;
        return context.RouteData.Values.TryGetValue(key, out var raw) && Guid.TryParse(raw?.ToString(), out value);
    }
}
