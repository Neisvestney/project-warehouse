using System.Threading.Channels;
using ProjectWarehouse.Server.Domain;

namespace ProjectWarehouse.Server.Integrations.Sync;

public record MarketplaceSyncRequest(Guid AccountId, Guid SyncRunId, MarketplaceSyncScope Scope);

public interface IMarketplaceSyncQueue
{
    ValueTask EnqueueAsync(MarketplaceSyncRequest request, CancellationToken ct);

    IAsyncEnumerable<MarketplaceSyncRequest> ReadAllAsync(CancellationToken ct);
}

public class MarketplaceSyncQueue : IMarketplaceSyncQueue
{
    // SingleReader serializes runs, so the advisory lock is a safety net rather than the load-bearing part
    private readonly Channel<MarketplaceSyncRequest> _channel =
        Channel.CreateBounded<MarketplaceSyncRequest>(new BoundedChannelOptions(200)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
        });

    public ValueTask EnqueueAsync(MarketplaceSyncRequest request, CancellationToken ct) =>
        _channel.Writer.WriteAsync(request, ct);

    public IAsyncEnumerable<MarketplaceSyncRequest> ReadAllAsync(CancellationToken ct) =>
        _channel.Reader.ReadAllAsync(ct);
}
