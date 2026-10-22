using Microsoft.EntityFrameworkCore;
using ProjectWarehouse.Server.Data;
using ProjectWarehouse.Server.Domain;
using ProjectWarehouse.Server.Models.Orders;

namespace ProjectWarehouse.Server.Services;

public class AssemblyPickHintService(ApplicationDbContext db) : IAssemblyPickHintService
{
    private readonly Dictionary<Guid, List<(Guid Id, CatalogItemType Type)>> _bundleComponents = [];
    private readonly Dictionary<Guid, List<(Guid Id, CatalogItemType Type)>> _variationMembers = [];

    /// <summary>Nodes whose children a full preload pass has already walked, as opposed to just cached.</summary>
    private readonly HashSet<Guid> _expanded = [];

    public async Task<IReadOnlyList<AssemblyLastPickDto>> GetLastPicksAsync(
        Guid? userId, IReadOnlyList<AssemblyLastPicksItemRequest> items, CancellationToken ct = default)
    {
        var requested = items.Select(i => (i.CatalogItemId, i.WarehouseId)).Distinct().ToList();
        var itemIds = requested.Select(r => r.CatalogItemId).Distinct().ToList();
        var types = await db.CatalogItems
            .AsNoTracking()
            .Where(i => itemIds.Contains(i.Id))
            .ToDictionaryAsync(i => i.Id, i => i.Type, ct);

        requested.RemoveAll(r => !types.TryGetValue(r.CatalogItemId, out var t)
                                 || t is not (CatalogItemType.Variation or CatalogItemType.Bundle));
        if (requested.Count == 0)
            return [];

        var lastByKey = await LoadLastFulfillmentsAsync(userId, requested, ct);

        // A hit variation is walked only down its membership to the resolved item, so the variants it did not
        // resolve to are not unfolded; a bundle's composition is matched against every variant, so it is in full
        var hitVariationIds = lastByKey.Keys
            .Select(k => k.CatalogItemId)
            .Where(id => types[id] == CatalogItemType.Variation)
            .Distinct()
            .ToList();
        await PreloadGraphAsync(hitVariationIds, variationsOnly: true, ct);
        await PreloadGraphAsync(lastByKey
            .Select(p => types[p.Key.CatalogItemId] == CatalogItemType.Bundle
                ? p.Key.CatalogItemId
                : p.Value.ResolvedCatalogItemId)
            .OfType<Guid>()
            .Distinct()
            .ToList(), variationsOnly: false, ct);

        var hints = new List<AssemblyLastPickDto>();
        foreach (var (catalogItemId, warehouseId) in requested)
        {
            var type = types[catalogItemId];
            if (!lastByKey.TryGetValue((catalogItemId, warehouseId), out var last))
                continue;

            var leaves = last.Leaves;
            List<AssemblyVariantStepDto> chain = [];
            var bundleId = catalogItemId;

            if (type == CatalogItemType.Variation)
            {
                if (last.ResolvedCatalogItemId is not { } resolvedId
                    || await FindChainAsync(catalogItemId, resolvedId, [], ct) is not { } found)
                    continue;

                chain = found;
                if (chain[^1].Type != CatalogItemType.Bundle)
                {
                    hints.Add(new AssemblyLastPickDto { CatalogItemId = catalogItemId, WarehouseId = warehouseId, Chain = chain });
                    continue;
                }

                bundleId = resolvedId;
            }

            var picks = await MatchBundleAsync(bundleId, "", leaves, [], ct);

            // A bundle whose composition changed since keeps the variant chain but loses its own picks
            if (picks is null && chain.Count == 0)
                continue;

            hints.Add(new AssemblyLastPickDto
            {
                CatalogItemId = catalogItemId,
                WarehouseId = warehouseId,
                Chain = chain,
                BundlePicks = picks?
                    .Select(p => new AssemblyBundlePickDto { Path = p.Key, Variant = p.Value })
                    .ToList() ?? [],
            });
        }

        return hints;
    }

    private sealed record LastFulfillment(Guid? ResolvedCatalogItemId, HashSet<Guid> Leaves);

    private async Task<Dictionary<(Guid CatalogItemId, Guid WarehouseId), LastFulfillment>> LoadLastFulfillmentsAsync(
        Guid? userId, IReadOnlyCollection<(Guid CatalogItemId, Guid WarehouseId)> requested, CancellationToken ct)
    {
        var itemIds = requested.Select(r => r.CatalogItemId).Distinct().ToList();
        var warehouseIds = requested.Select(r => (Guid?)r.WarehouseId).Distinct().ToList();

        // Unit picks name one physical item that has since left the shelf, so they are no precedent. The number
        // is what marks one: deleting the unit nulls the id but keeps the number
        var lastIds = db.AssemblyFulfillments
            .Where(f => (userId == null || f.CreatedById == userId)
                        && f.UnitInventoryItemId == null && f.UnitInventoryNumber == null
                        && itemIds.Contains(f.TaskBoxComponent.CatalogItemId)
                        && warehouseIds.Contains(f.TaskBoxComponent.AssemblyTaskBox.AssemblyTask.Order.WarehouseId))
            .GroupBy(f => new
            {
                f.TaskBoxComponent.CatalogItemId,
                f.TaskBoxComponent.AssemblyTaskBox.AssemblyTask.Order.WarehouseId,
            })
            .Select(g => g.OrderByDescending(f => f.CreatedAt).ThenByDescending(f => f.Id).Select(f => f.Id).First());

        var rows = await db.AssemblyFulfillments
            .AsNoTracking()
            .Where(f => lastIds.Contains(f.Id))
            .Select(f => new
            {
                f.TaskBoxComponent.CatalogItemId,
                WarehouseId = f.TaskBoxComponent.AssemblyTaskBox.AssemblyTask.Order.WarehouseId!.Value,
                f.ResolvedCatalogItemId,
                Leaves = f.BundleComponents.Select(b => b.CatalogItemId).ToList(),
            })
            .ToListAsync(ct);

        // Items and warehouses were filtered as a cross product, so drop the pairs nobody asked for
        return rows
            .Where(r => requested.Contains((r.CatalogItemId, r.WarehouseId)))
            .ToDictionary(
                r => (r.CatalogItemId, r.WarehouseId),
                r => new LastFulfillment(r.ResolvedCatalogItemId, r.Leaves.ToHashSet()));
    }

    /// <summary>
    /// Fills the composition caches for everything reachable from <paramref name="rootIds"/>, one level per round
    /// trip; <paramref name="variationsOnly"/> follows only variation members that are variations themselves.
    /// </summary>
    private async Task PreloadGraphAsync(IReadOnlyCollection<Guid> rootIds, bool variationsOnly, CancellationToken ct)
    {
        bool Done(Guid id) => variationsOnly ? _bundleComponents.ContainsKey(id) : _expanded.Contains(id);

        var frontier = rootIds.Where(id => !Done(id)).ToList();
        while (frontier.Count > 0)
        {
            // A node cached by an earlier pass or lazily still has its children walked, but is not queried again
            var toLoad = frontier.Where(id => !_bundleComponents.ContainsKey(id)).ToList();
            var components = (await db.BundleComponents
                    .AsNoTracking()
                    .Where(c => toLoad.Contains(c.BundleId))
                    .OrderBy(c => c.Order)
                    .Select(c => new { ParentId = c.BundleId, Id = c.ComponentId, c.Component.Type })
                    .ToListAsync(ct))
                .ToLookup(c => c.ParentId, c => (c.Id, c.Type));
            var members = (await db.CatalogItemVariationMembers
                    .AsNoTracking()
                    .Where(m => toLoad.Contains(m.VariationId))
                    .Select(m => new { ParentId = m.VariationId, Id = m.ItemId, m.Item.Type })
                    .ToListAsync(ct))
                .ToLookup(m => m.ParentId, m => (m.Id, m.Type));

            foreach (var id in toLoad)
            {
                _bundleComponents[id] = components[id].ToList();
                _variationMembers[id] = members[id].ToList();
            }

            if (!variationsOnly)
                _expanded.UnionWith(frontier);

            frontier = frontier
                .SelectMany(id => _bundleComponents[id].Concat(_variationMembers.GetValueOrDefault(id) ?? []))
                .Where(c => (variationsOnly
                                ? c.Type == CatalogItemType.Variation
                                : c.Type is CatalogItemType.Bundle or CatalogItemType.Variation)
                            && !Done(c.Id))
                .Select(c => c.Id)
                .Distinct()
                .ToList();
        }
    }

    /// <summary>Variant steps from <paramref name="variationId"/> down to <paramref name="targetId"/>, or null when it is no member.</summary>
    private async Task<List<AssemblyVariantStepDto>?> FindChainAsync(
        Guid variationId, Guid targetId, HashSet<Guid> visiting, CancellationToken ct)
    {
        if (!visiting.Add(variationId))
            return null;

        foreach (var member in await MembersAsync(variationId, ct))
        {
            var step = new AssemblyVariantStepDto { Id = member.Id, Type = member.Type };
            if (member.Id == targetId)
                return [step];

            if (member.Type == CatalogItemType.Variation
                && await FindChainAsync(member.Id, targetId, visiting, ct) is { } rest)
                return [step, .. rest];
        }

        visiting.Remove(variationId);
        return null;
    }

    /// <summary>
    /// Picks for every variation slot under <paramref name="bundleId"/> that lead to the recorded leaves, keyed
    /// the way the client keys composition slots. Null when some leaf of the current composition was not picked
    /// last time, i.e. the bundle changed since.
    /// </summary>
    private async Task<Dictionary<string, AssemblyVariantStepDto>?> MatchBundleAsync(
        Guid bundleId, string path, HashSet<Guid> leaves, HashSet<Guid> visiting, CancellationToken ct)
    {
        if (!visiting.Add(bundleId))
            return null;

        var picks = new Dictionary<string, AssemblyVariantStepDto>();
        foreach (var component in await ComponentsAsync(bundleId, ct))
        {
            var slot = SlotPath(path, component.Id);
            var matched = component.Type switch
            {
                CatalogItemType.Standard or CatalogItemType.Unit =>
                    leaves.Contains(component.Id) ? new Dictionary<string, AssemblyVariantStepDto>() : null,
                CatalogItemType.Bundle => await MatchBundleAsync(component.Id, slot, leaves, visiting, ct),
                CatalogItemType.Variation => await MatchVariationAsync(component.Id, slot, leaves, visiting, ct),
                _ => null,
            };

            if (matched is null)
            {
                visiting.Remove(bundleId);
                return null;
            }

            foreach (var (key, value) in matched)
                picks[key] = value;
        }

        visiting.Remove(bundleId);
        return picks;
    }

    /// <summary>The first variant of the slot at <paramref name="path"/> whose own subtree matches the recorded leaves.</summary>
    private async Task<Dictionary<string, AssemblyVariantStepDto>?> MatchVariationAsync(
        Guid variationId, string path, HashSet<Guid> leaves, HashSet<Guid> visiting, CancellationToken ct)
    {
        if (!visiting.Add(variationId))
            return null;

        foreach (var member in await MembersAsync(variationId, ct))
        {
            // Everything under a picked variant hangs off a path that names it, as the client keys it
            var below = SlotPath(path, member.Id);
            var nested = member.Type switch
            {
                CatalogItemType.Standard or CatalogItemType.Unit =>
                    leaves.Contains(member.Id) ? new Dictionary<string, AssemblyVariantStepDto>() : null,
                CatalogItemType.Bundle => await MatchBundleAsync(member.Id, below, leaves, visiting, ct),
                CatalogItemType.Variation => await MatchVariationAsync(member.Id, below, leaves, visiting, ct),
                _ => null,
            };

            if (nested is null)
                continue;

            nested[path] = new AssemblyVariantStepDto { Id = member.Id, Type = member.Type };
            visiting.Remove(variationId);
            return nested;
        }

        visiting.Remove(variationId);
        return null;
    }

    private static string SlotPath(string parent, Guid segment) =>
        parent.Length > 0 ? $"{parent}/{segment}" : segment.ToString();

    private async Task<List<(Guid Id, CatalogItemType Type)>> ComponentsAsync(Guid bundleId, CancellationToken ct)
    {
        if (_bundleComponents.TryGetValue(bundleId, out var cached))
            return cached;

        var components = (await db.BundleComponents
                .AsNoTracking()
                .Where(c => c.BundleId == bundleId)
                .OrderBy(c => c.Order)
                .Select(c => new { c.ComponentId, c.Component.Type })
                .ToListAsync(ct))
            .Select(c => (c.ComponentId, c.Type))
            .ToList();

        return _bundleComponents[bundleId] = components;
    }

    private async Task<List<(Guid Id, CatalogItemType Type)>> MembersAsync(Guid variationId, CancellationToken ct)
    {
        if (_variationMembers.TryGetValue(variationId, out var cached))
            return cached;

        var members = (await db.CatalogItemVariationMembers
                .AsNoTracking()
                .Where(m => m.VariationId == variationId)
                .Select(m => new { m.ItemId, m.Item.Type })
                .ToListAsync(ct))
            .Select(m => (m.ItemId, m.Type))
            .ToList();

        return _variationMembers[variationId] = members;
    }
}
