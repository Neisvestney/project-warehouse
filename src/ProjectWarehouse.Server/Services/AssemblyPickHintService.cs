using Microsoft.EntityFrameworkCore;
using ProjectWarehouse.Server.Data;
using ProjectWarehouse.Server.Domain;
using ProjectWarehouse.Server.Models.Orders;

namespace ProjectWarehouse.Server.Services;

public class AssemblyPickHintService(ApplicationDbContext db) : IAssemblyPickHintService
{
    private readonly Dictionary<Guid, List<(Guid Id, CatalogItemType Type)>> _bundleComponents = [];
    private readonly Dictionary<Guid, List<(Guid Id, CatalogItemType Type)>> _variationMembers = [];

    public async Task<IReadOnlyList<AssemblyLastPickDto>> GetLastPicksAsync(
        Guid userId, IReadOnlyList<AssemblyLastPicksItemRequest> items, CancellationToken ct = default)
    {
        var requested = items.Select(i => (i.CatalogItemId, i.WarehouseId)).Distinct().ToList();
        var itemIds = requested.Select(r => r.CatalogItemId).Distinct().ToList();
        var types = await db.CatalogItems
            .AsNoTracking()
            .Where(i => itemIds.Contains(i.Id))
            .ToDictionaryAsync(i => i.Id, i => i.Type, ct);

        var hints = new List<AssemblyLastPickDto>();
        foreach (var (catalogItemId, warehouseId) in requested)
        {
            if (!types.TryGetValue(catalogItemId, out var type)
                || type is not (CatalogItemType.Variation or CatalogItemType.Bundle))
                continue;

            // Unit picks name one physical item that has since left the shelf, so they are no precedent
            var last = await db.AssemblyFulfillments
                .AsNoTracking()
                .Where(f => f.CreatedById == userId && f.UnitInventoryItemId == null
                            && f.TaskBoxComponent.CatalogItemId == catalogItemId
                            && f.TaskBoxComponent.AssemblyTaskBox.AssemblyTask.Order.WarehouseId == warehouseId)
                .OrderByDescending(f => f.CreatedAt)
                .Select(f => new
                {
                    f.ResolvedCatalogItemId,
                    Leaves = f.BundleComponents.Select(b => b.CatalogItemId).ToList(),
                })
                .FirstOrDefaultAsync(ct);

            if (last is null)
                continue;

            var leaves = last.Leaves.ToHashSet();
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
