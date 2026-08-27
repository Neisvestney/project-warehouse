using System.Diagnostics;

namespace ProjectWarehouse.Server.Infrastructure.Observability;

/// <summary>
/// Spans the application opens itself, alongside those the instrumentation libraries produce.
/// <see cref="ActivitySourceName" /> has to be registered with <c>AddSource</c> or none of them is exported.
/// </summary>
public static class AppTelemetry
{
    public const string ActivitySourceName = "ProjectWarehouse.Server";

    public static readonly ActivitySource Source = new(ActivitySourceName);

    /// <summary>
    /// Opens the span covering one message taken off an in-process queue. The producer is attached as a
    /// link rather than as a parent — the consumer runs on its own schedule and outlives it.
    /// <para>
    /// Detached: the ambient <see cref="Activity.Current" /> is dropped before the span starts, so
    /// everything downstream hangs off the returned span alone. Call it only from a consumer loop, never
    /// from a flow whose ambient context still has to survive.
    /// </para>
    /// </summary>
    public static Activity? StartDetachedQueueConsumer(string name, in TraceContextCarrier trace)
    {
        // the link is the only tie to the producer; an ambient activity would quietly make this a child
        Activity.Current = null;
        return Source.StartActivity(name, ActivityKind.Consumer, parentContext: default, links: trace.ToLinks());
    }
}
