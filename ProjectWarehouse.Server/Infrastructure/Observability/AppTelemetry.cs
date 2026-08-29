using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace ProjectWarehouse.Server.Infrastructure.Observability;

/// <summary>
/// Spans and instruments the application opens itself, alongside those the instrumentation libraries
/// produce. <see cref="ActivitySourceName" /> has to be registered with <c>AddSource</c> and
/// <see cref="MeterName" /> with <c>AddMeter</c>, or none of them is exported.
/// </summary>
public static class AppTelemetry
{
    public const string ActivitySourceName = "ProjectWarehouse.Server";

    public static readonly ActivitySource Source = new(ActivitySourceName);

    public const string MeterName = "ProjectWarehouse.Server";

    /// <summary>
    /// Meter behind the application's own instruments. Registered with <c>AddMeter</c> next to the
    /// activity source, and just as invisible without it.
    /// </summary>
    public static readonly Meter Meter = new(MeterName);

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
