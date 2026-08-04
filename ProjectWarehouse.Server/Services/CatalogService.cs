using Microsoft.EntityFrameworkCore;
using ProjectWarehouse.Server.Data;
using ProjectWarehouse.Server.Domain;
using ProjectWarehouse.Server.Infrastructure;

namespace ProjectWarehouse.Server.Services;

public class CatalogService(ApplicationDbContext db) : ICatalogService
{
    public async Task EnsureNoCycleAsync(
        Guid rootId,
        CatalogItemType rootType,
        IReadOnlyList<Guid> rootEdgeIds,
        CancellationToken ct = default)
    {
        var stack = new HashSet<Guid> { rootId };
        await VisitAsync(rootType, rootEdgeIds, stack, ct);
    }

    /// <summary>
    /// Visits the "internal" nodes (Bundle or Variation) reachable from <paramref name="edgeIds"/>
    /// that are of the opposite internal type — the only edges that can lead deeper into the
    /// graph — and recurses. <paramref name="stack"/> holds the IDs on the current path;
    /// revisiting one of them means a cycle.
    /// </summary>
    private async Task VisitAsync(
        CatalogItemType nodeType,
        IReadOnlyList<Guid> edgeIds,
        HashSet<Guid> stack,
        CancellationToken ct)
    {
        if (edgeIds.Count == 0) return;

        var nextType = nodeType == CatalogItemType.Bundle ? CatalogItemType.Variation : CatalogItemType.Bundle;

        var nextNodeIds = await db.CatalogItems
            .Where(x => edgeIds.Contains(x.Id) && x.Type == nextType)
            .Select(x => x.Id)
            .ToListAsync(ct);

        foreach (var nodeId in nextNodeIds)
        {
            if (!stack.Add(nodeId))
                throw new BundleCircularDependencyException();

            var childEdgeIds = nextType == CatalogItemType.Bundle
                ? await db.BundleComponents
                    .Where(bc => bc.BundleId == nodeId)
                    .Select(bc => bc.ComponentId)
                    .ToListAsync(ct)
                : await db.CatalogItemVariationMembers
                    .Where(vm => vm.VariationId == nodeId)
                    .Select(vm => vm.ItemId)
                    .ToListAsync(ct);

            await VisitAsync(nextType, childEdgeIds, stack, ct);

            stack.Remove(nodeId);
        }
    }

    public async Task<Dictionary<Guid, bool>> ComputeContainsUnitAsync(
        IReadOnlyCollection<Guid> catalogItemIds,
        CancellationToken ct = default)
    {
        var cache = new Dictionary<Guid, bool>();
        foreach (var id in catalogItemIds)
        {
            if (!cache.ContainsKey(id))
                await ComputeContainsUnitRecursiveAsync(id, cache, [], ct);
        }
        return cache;
    }

    /// <summary>
    /// Depth-first, memoized walk mirroring <see cref="VisitAsync"/>'s traversal shape, but
    /// unlike the cycle check this also inspects leaf (Standard/Unit/ProductGroup) edges, since
    /// a Bundle or Variation can directly contain a Unit leaf without any further nesting.
    /// </summary>
    private async Task<bool> ComputeContainsUnitRecursiveAsync(
        Guid catalogItemId, Dictionary<Guid, bool> cache, HashSet<Guid> visiting, CancellationToken ct)
    {
        if (cache.TryGetValue(catalogItemId, out var cached))
            return cached;

        // Defensive cycle guard — writes should already prevent this via EnsureNoCycleAsync.
        if (!visiting.Add(catalogItemId))
            return false;

        var type = await db.CatalogItems
            .Where(c => c.Id == catalogItemId)
            .Select(c => c.Type)
            .FirstOrDefaultAsync(ct);

        bool result;
        if (type == CatalogItemType.Unit)
        {
            result = true;
        }
        else if (type == CatalogItemType.Bundle)
        {
            var children = await db.BundleComponents
                .Where(bc => bc.BundleId == catalogItemId)
                .Select(bc => new { bc.ComponentId, bc.Component.Type })
                .ToListAsync(ct);

            result = false;
            foreach (var child in children)
            {
                if (child.Type == CatalogItemType.Unit) { result = true; break; }
                if (child.Type is CatalogItemType.Bundle or CatalogItemType.Variation
                    && await ComputeContainsUnitRecursiveAsync(child.ComponentId, cache, visiting, ct))
                { result = true; break; }
            }
        }
        else if (type == CatalogItemType.Variation)
        {
            var members = await db.CatalogItemVariationMembers
                .Where(vm => vm.VariationId == catalogItemId)
                .Select(vm => new { ComponentId = vm.ItemId, vm.Item.Type })
                .ToListAsync(ct);

            result = false;
            foreach (var member in members)
            {
                if (member.Type == CatalogItemType.Unit) { result = true; break; }
                if (member.Type is CatalogItemType.Bundle or CatalogItemType.Variation
                    && await ComputeContainsUnitRecursiveAsync(member.ComponentId, cache, visiting, ct))
                { result = true; break; }
            }
        }
        else
        {
            result = false; // Standard, ProductGroup
        }

        visiting.Remove(catalogItemId);
        cache[catalogItemId] = result;
        return result;
    }

    public async Task<bool> IsVariationMemberAsync(
        Guid variationId, Guid candidateId, CancellationToken ct = default)
    {
        var members = new HashSet<Guid>();
        await CollectVariationMembersAsync(variationId, members, [], ct);
        return members.Contains(candidateId);
    }

    /// <summary>
    /// Collects the items a Variation can resolve to. Nested Variations are expanded further;
    /// Standard/Unit/Bundle members are terminal, since a Bundle member is itself a valid choice.
    /// </summary>
    private async Task CollectVariationMembersAsync(
        Guid variationId, HashSet<Guid> members, HashSet<Guid> visiting, CancellationToken ct)
    {
        // Defensive cycle guard — writes should already prevent this via EnsureNoCycleAsync.
        if (!visiting.Add(variationId)) return;

        var directMembers = await db.CatalogItemVariationMembers
            .Where(vm => vm.VariationId == variationId)
            .Select(vm => new { vm.ItemId, vm.Item.Type })
            .ToListAsync(ct);

        foreach (var member in directMembers)
        {
            if (member.Type == CatalogItemType.Variation)
                await CollectVariationMembersAsync(member.ItemId, members, visiting, ct);
            else
                members.Add(member.ItemId);
        }

        visiting.Remove(variationId);
    }
}
