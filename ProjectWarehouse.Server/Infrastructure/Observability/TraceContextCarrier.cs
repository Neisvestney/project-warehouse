using System.Diagnostics;

namespace ProjectWarehouse.Server.Infrastructure.Observability;

/// <summary>
/// W3C trace context of whatever produced a message, kept in its wire form: strings survive serialization
/// and a durable queue, an <see cref="ActivityContext" /> does not.
/// </summary>
public readonly record struct TraceContextCarrier(string? TraceParent, string? TraceState)
{
    public static TraceContextCarrier Capture()
    {
        var activity = Activity.Current;
        return activity is null ? default : new TraceContextCarrier(activity.Id, activity.TraceStateString);
    }

    public bool TryGetContext(out ActivityContext context) =>
        ActivityContext.TryParse(TraceParent, TraceState, isRemote: true, out context);

    public ActivityLink[] ToLinks() => TryGetContext(out var context) ? [new ActivityLink(context)] : [];
}
