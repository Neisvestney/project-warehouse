using System.Threading.Channels;

namespace ProjectWarehouse.Server.Infrastructure.Realtime;

/// <summary>
/// One open SSE stream. Holds the user identity the stream was opened with — no permission claims:
/// every right is checked in the ordinary HTTP requests that command the subsystem.
/// </summary>
public sealed class RealtimeConnection : IDisposable
{
    private const int BufferCapacity = 64;

    private readonly Channel<RealtimeEvent> _events = Channel.CreateBounded<RealtimeEvent>(
        new BoundedChannelOptions(BufferCapacity)
        {
            // Wait rather than DropWrite: with DropWrite TryWrite reports success and loses the event,
            // leaving a stalled reader undetectable.
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
        });

    private readonly CancellationTokenSource _abort = new();

    private long _lastSeenTicks = DateTime.UtcNow.Ticks;

    public required Guid Id { get; init; }

    public required Guid UserId { get; init; }

    public required string UserName { get; init; }

    public ChannelReader<RealtimeEvent> Reader => _events.Reader;

    public CancellationToken Aborted => _abort.Token;

    /// <summary>
    /// Last client heartbeat. The stream itself proves nothing: a proxy that outlives the browser keeps
    /// the socket open and writes to it keep succeeding, so liveness has to come from the client asking.
    /// </summary>
    public DateTime LastSeenAt => new(Interlocked.Read(ref _lastSeenTicks), DateTimeKind.Utc);

    public void Touch() => Interlocked.Exchange(ref _lastSeenTicks, DateTime.UtcNow.Ticks);

    /// <summary>False when the buffer is full — the client is not keeping up and the stream is closed.</summary>
    public bool TryEnqueue(RealtimeEvent evt) => _events.Writer.TryWrite(evt);

    public void Abort()
    {
        _events.Writer.TryComplete();

        try
        {
            _abort.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }
    }

    public void Dispose() => _abort.Dispose();
}
