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
}
