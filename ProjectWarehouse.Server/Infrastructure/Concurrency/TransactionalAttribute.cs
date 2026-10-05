using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using ProjectWarehouse.Server.Data;
using ProjectWarehouse.Server.Infrastructure.Observability;
using ProjectWarehouse.Server.Infrastructure.Realtime;
using ProjectWarehouse.Server.Models;
using ProjectWarehouse.Server.Services;

namespace ProjectWarehouse.Server.Infrastructure.Concurrency;

/// <summary>
/// Runs the action as one transaction: committed on a 2xx result, rolled back on anything else, so a
/// request rejected halfway leaves nothing behind. Locks taken through <see cref="IEntityLockService" />
/// inside the action are held until that commit. Realtime events published meanwhile wait in a
/// <see cref="RealtimeOutbox" /> and go out only after the commit.
/// <para>
/// Outer filters such as <see cref="PublishesEntityChangedAttribute" /> run after the commit and see committed
/// state; only <see cref="PublishesAssemblyChangedAttribute" /> sits inside, so its snapshot is taken under the lock.
/// </para>
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public class TransactionalAttribute : Attribute, IAsyncActionFilter, IOrderedFilter
{
    public const int FilterOrder = int.MaxValue - 1;

    public int Order => FilterOrder;

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var http = context.HttpContext;
        var services = http.RequestServices;
        var db = services.GetRequiredService<ApplicationDbContext>();
        var ct = http.RequestAborted;

        // a second transactional filter would nest on a savepoint and flush its events before the real commit
        if (RealtimeOutbox.Current(http) is not null)
            throw new InvalidOperationException(
                $"'{context.ActionDescriptor.DisplayName}' carries more than one transactional filter.");

        var outbox = RealtimeOutbox.Open(http);
        ActionExecutedContext? executed = null;

        try
        {
            await db.Database.ExecuteInTransactionAsync(OperationName(context), async () =>
            {
                await AcquireAsync(context, services.GetRequiredService<IEntityLockService>(), ct);

                executed = await next();

                if (executed.Exception is EntityLockedException && !executed.ExceptionHandled)
                {
                    executed.Result = LockedResult();
                    executed.ExceptionHandled = true;
                }

                if (!Succeeded(executed))
                    throw new ActionRejectedException();
            }, ct);
        }
        catch (ActionRejectedException)
        {
            outbox.Discard(http);
            return;
        }
        catch (EntityLockedException) when (executed is null)
        {
            outbox.Discard(http);
            context.Result = LockedResult();
            return;
        }
        catch
        {
            outbox.Discard(http);
            throw;
        }

        await outbox.FlushAsync(http, services.GetRequiredService<IRealtimeNotifier>(), ct);
    }

    /// <summary>Runs right after BEGIN, before the action loads anything.</summary>
    protected virtual Task AcquireAsync(ActionExecutingContext context, IEntityLockService locks, CancellationToken ct) =>
        Task.CompletedTask;

    private static bool Succeeded(ActionExecutedContext executed) =>
        (executed.Exception is null || executed.ExceptionHandled)
        && executed.Result is not IStatusCodeActionResult { StatusCode: < 200 or >= 300 };

    private static ObjectResult LockedResult()
    {
        var details = AppProblems.Conflict(ErrorCode.EntityLocked,
            "The object is being changed by another request; nothing was written.");
        return new ObjectResult(details) { StatusCode = details.Status };
    }

    private static string OperationName(ActionExecutingContext context) =>
        context.ActionDescriptor is ControllerActionDescriptor action
            ? $"{Snake(action.ControllerName)}.{Snake(action.ActionName)}"
            : "http.action";

    private static string Snake(string name) => JsonNamingPolicy.SnakeCaseLower.ConvertName(name);

    /// <summary>Rolls the transaction back after the action has already produced its non-2xx result.</summary>
    private sealed class ActionRejectedException : Exception, IExpectedFailure;
}
