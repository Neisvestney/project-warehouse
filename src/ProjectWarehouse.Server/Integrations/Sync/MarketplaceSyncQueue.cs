using System.Threading.Channels;
using ProjectWarehouse.Server.Domain;
using ProjectWarehouse.Server.Infrastructure.Observability;

namespace ProjectWarehouse.Server.Integrations.Sync;

public record MarketplaceSyncRequest(Guid AccountId, Guid SyncRunId, MarketplaceSyncScope Scope);

public interface IMarketplaceSyncQueue
{
    ValueTask EnqueueAsync(MarketplaceSyncRequest request, CancellationToken ct);

    IAsyncEnumerable<TracedMessage<MarketplaceSyncRequest>> ReadAllAsync(CancellationToken ct);
}

public class MarketplaceSyncQueue : IMarketplaceSyncQueue
{
    // SingleReader serializes runs, so the advisory lock is a safety net rather than the load-bearing part
    private readonly Channel<TracedMessage<MarketplaceSyncRequest>> _channel =
        Channel.CreateBounded<TracedMessage<MarketplaceSyncRequest>>(new BoundedChannelOptions(200)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
        });

    // captured here rather than at the call sites: enqueueing always happens inside the span that wants the link
    public ValueTask EnqueueAsync(MarketplaceSyncRequest request, CancellationToken ct) =>
        _channel.Writer.WriteAsync(TracedMessage<MarketplaceSyncRequest>.Capture(request), ct);

    public IAsyncEnumerable<TracedMessage<MarketplaceSyncRequest>> ReadAllAsync(CancellationToken ct) =>
        _channel.Reader.ReadAllAsync(ct);
}
