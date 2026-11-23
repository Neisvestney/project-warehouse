using System.Collections.Concurrent;
using Microsoft.Extensions.Options;
using ProjectWarehouse.Server.Infrastructure.Marketplaces;

namespace ProjectWarehouse.Server.Integrations.YandexMarket;

public sealed class YandexMarketReportQuota(IOptions<MarketplacesOptions> options)
{
    private sealed class Slot
    {
        public readonly SemaphoreSlim Gate = new(1);
        public long NextAtTicks;
    }

    private readonly ConcurrentDictionary<long, Slot> _slots = new();

    public async Task WaitAsync(long businessId, CancellationToken ct)
    {
        var slot = _slots.GetOrAdd(businessId, _ => new Slot());
        await slot.Gate.WaitAsync(ct);
        try
        {
            while (new DateTime(Interlocked.Read(ref slot.NextAtTicks), DateTimeKind.Utc) - DateTime.UtcNow is { Ticks: > 0 } delay)
                await Task.Delay(delay, ct);
            Interlocked.Exchange(ref slot.NextAtTicks,
                DateTime.UtcNow.AddMilliseconds(Math.Max(120000, options.Value.YandexMarket.ReportRequestDelayMs)).Ticks);
        }
        finally
        {
            slot.Gate.Release();
        }
    }

    public void Refused(long businessId)
    {
        var slot = _slots.GetOrAdd(businessId, _ => new Slot());
        Interlocked.Exchange(ref slot.NextAtTicks, DateTime.UtcNow.AddMinutes(2).Ticks);
    }
}
