namespace ProjectWarehouse.Server.Infrastructure.Observability;

/// <summary>
/// A queued payload together with the trace context of the code that enqueued it, so the consumer's span
/// can point back at the request or job that asked for the work.
/// </summary>
public readonly record struct TracedMessage<T>(T Payload, TraceContextCarrier Trace)
{
    public static TracedMessage<T> Capture(T payload) => new(payload, TraceContextCarrier.Capture());
}
