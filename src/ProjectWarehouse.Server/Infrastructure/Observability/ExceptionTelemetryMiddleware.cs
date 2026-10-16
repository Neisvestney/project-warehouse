using System.Diagnostics;
using System.Text.Json;

namespace ProjectWarehouse.Server.Infrastructure.Observability;

/// <summary>
/// Attaches an exception that escaped the pipeline to the HTTP span. Without it a failed request leaves a
/// span with status 500 and nothing else, and the stack trace only ever reaches the log.
/// <para>
/// Recorded here rather than through <c>RecordException</c> on the ASP.NET Core instrumentation so the
/// <see cref="IExpectedFailure" /> split of <see cref="TransactionTracing" /> holds at the edge too, and so
/// a stack trace lands on the span exactly once. The exception is rethrown untouched — the response and its
/// status stay whatever the pipeline would have produced.
/// </para>
/// </summary>
public class ExceptionTelemetryMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception e)
        {
            Record(Activity.Current, context, e);
            throw;
        }
    }

    private static void Record(Activity? activity, HttpContext context, Exception e)
    {
        if (activity is null) return;

        activity.SetTag("exception.type", e.GetType().Name);

        // a client that hung up is not a failure of ours, and it is the common shape of a cancelled request
        if (e is OperationCanceledException && context.RequestAborted.IsCancellationRequested)
        {
            activity.SetTag("http.request.outcome", "cancelled");
            return;
        }

        // A business failure is normally caught by the controller and never gets here; one that does is
        // already a 500, and the ASP.NET Core instrumentation marks the span accordingly. The reason is
        // recorded anyway — the alternative is a 500 whose span says only that something threw.
        if (e is IExpectedFailure expected)
        {
            activity.SetTag("http.request.outcome", "rejected");
            if (expected.Code is { } code)
                activity.SetTag("error.code", JsonNamingPolicy.CamelCase.ConvertName(code.ToString()));
            return;
        }

        activity.AddException(e);
        activity.SetStatus(ActivityStatusCode.Error, e.Message);
    }
}
