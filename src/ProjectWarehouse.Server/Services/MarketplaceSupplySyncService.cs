using Microsoft.EntityFrameworkCore;
using ProjectWarehouse.Server.Data;
using ProjectWarehouse.Server.Domain;
using ProjectWarehouse.Server.Infrastructure.Observability;
using ProjectWarehouse.Server.Integrations.Abstractions;

namespace ProjectWarehouse.Server.Services;

/// <remarks>
/// <see cref="MarketplaceOrder.SupplyId"/> is written only here and only with guarded single-column updates, so
/// neither method takes an order lock: no user request writes the column.
/// </remarks>
public class MarketplaceSupplySyncService(ApplicationDbContext db) : IMarketplaceSupplySyncService
{
    public async Task SyncSuppliesAsync(IMarketplaceProvider provider, MarketplaceCredentials credentials,
        MarketplaceAccount account, CancellationToken ct)
    {
        if (!provider.Capabilities.HasFlag(MarketplaceCapabilities.Supplies))
            return;

        using var activity = AppTelemetry.Source.StartActivity("marketplace.sync.supplies");

        var listed = await provider.FetchSuppliesAsync(credentials, ct);
        var listedIds = listed.Select(s => s.ExternalId).ToHashSet(StringComparer.Ordinal);
        var listedIdList = listedIds.ToList();

        // done supplies outside the listing can change no more, so they are not loaded
        var known = await db.MarketplaceSupplies
            .Where(s => s.MarketplaceAccountId == account.Id && (!s.IsDone || listedIdList.Contains(s.ExternalId)))
            .ToDictionaryAsync(s => s.ExternalId, StringComparer.Ordinal, ct);

        foreach (var external in listed)
        {
            if (!known.TryGetValue(external.ExternalId, out var supply))
            {
                supply = new MarketplaceSupply
                {
                    Id = Guid.NewGuid(),
                    MarketplaceAccountId = account.Id,
                    ExternalId = external.ExternalId,
                };
                db.MarketplaceSupplies.Add(supply);
                known[external.ExternalId] = supply;
            }

            // read once more after closing: the last read of an open supply may predate its final composition
            if (external.IsDone && !supply.IsDone)
                supply.OrdersSyncedAt = null;

            supply.Name = external.Name;
            supply.CreatedAt = external.CreatedAt;
            supply.ClosedAt = external.ClosedAt;
            supply.IsDone = external.IsDone;
        }

        // only an empty supply can be deleted on the marketplace; the FK unlinks whatever WMS still had in it.
        // An empty listing is distrusted: it would wipe every open supply at once.
        if (listed.Count > 0)
            db.MarketplaceSupplies.RemoveRange(
                known.Values.Where(s => !s.IsDone && !listedIds.Contains(s.ExternalId)));
        await db.SaveChangesAsync(ct);

        var toRead = known.Values
            .Where(s => listedIds.Contains(s.ExternalId) && (!s.IsDone || s.OrdersSyncedAt == null))
            .ToDictionary(s => s.ExternalId, StringComparer.Ordinal);
        activity?.SetTag("marketplace.supplies.listed", listed.Count);
        activity?.SetTag("marketplace.supplies.read", toRead.Count);
        if (toRead.Count == 0)
            return;

        var answered = new HashSet<string>(StringComparer.Ordinal);
        await foreach (var composition in provider.FetchSupplyPostingsAsync(credentials, [.. toRead.Keys], ct))
        {
            if (!toRead.TryGetValue(composition.SupplyExternalId, out var supply))
                continue;
            answered.Add(composition.SupplyExternalId);

            var numbers = composition.PostingNumbers.ToList();
            await db.Database.ExecuteInTransactionAsync("marketplace.sync.supplies.composition", async () =>
            {
                // a posting that left for another supply already points there, so the order of supplies is moot
                await db.MarketplaceOrders
                    .Where(o => o.SupplyId == supply.Id && !numbers.Contains(o.PostingNumber))
                    .ExecuteUpdateAsync(s => s.SetProperty(o => o.SupplyId, (Guid?)null), ct);
                await LinkAsync(account.Id, supply.Id, numbers, ct);

                supply.OrdersSyncedAt = DateTime.UtcNow;
                await db.SaveChangesAsync(ct);
            }, ct);
        }

        // a done supply cannot be deleted, so the marketplace will not answer for it next time either
        var unanswered = toRead.Values.Where(s => s.IsDone && !answered.Contains(s.ExternalId)).ToList();
        if (unanswered.Count > 0)
        {
            foreach (var supply in unanswered)
                supply.OrdersSyncedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
        }
    }

    public async Task LinkPostingsAsync(Guid accountId, IReadOnlyList<ExternalPosting> page, CancellationToken ct)
    {
        var groups = page
            .Where(p => p.SupplyExternalId is not null)
            .GroupBy(p => p.SupplyExternalId!, StringComparer.Ordinal)
            .ToList();
        if (groups.Count == 0)
            return;

        var externalIds = groups.Select(g => g.Key).ToList();
        var supplies = await db.MarketplaceSupplies
            .Where(s => s.MarketplaceAccountId == accountId && externalIds.Contains(s.ExternalId))
            .ToDictionaryAsync(s => s.ExternalId, s => s.Id, StringComparer.Ordinal, ct);

        foreach (var group in groups)
            if (supplies.TryGetValue(group.Key, out var supplyId))
                await LinkAsync(accountId, supplyId, [.. group.Select(p => p.PostingNumber)], ct);
    }

    private async Task LinkAsync(Guid accountId, Guid supplyId, List<string> postingNumbers, CancellationToken ct) =>
        await db.MarketplaceOrders
            .Where(o => o.MarketplaceAccountId == accountId
                        && postingNumbers.Contains(o.PostingNumber)
                        && o.SupplyId != supplyId)
            .ExecuteUpdateAsync(s => s.SetProperty(o => o.SupplyId, supplyId), ct);
}
