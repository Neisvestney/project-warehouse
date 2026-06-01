using System.Security.Cryptography;
using AutoMapper;
using Microsoft.EntityFrameworkCore;
using ProjectWarehouse.Server.Data;
using ProjectWarehouse.Server.Domain;
using ProjectWarehouse.Server.Infrastructure;
using ProjectWarehouse.Server.Infrastructure.ChangeLog;
using ProjectWarehouse.Server.Models.Catalog;

namespace ProjectWarehouse.Server.Services;

public class CatalogService(
    ApplicationDbContext db,
    IMapper mapper,
    IChangeLogService<CatalogItemDto> changeLog) : ICatalogService
{
    // Maximum number of combinations generated from a bundle's cartesian product.
    // Prevents OOM on deeply nested variation trees.
    private const int MaxCombinations = 500;

    // ── Public API ───────────────────────────────────────────────────────────

    public Task<IReadOnlyList<BundleComponentNode>> LoadBundleComponentsTreeAsync(
        Guid bundleId,
        Dictionary<Guid, CatalogItem> localCache,
        CancellationToken ct = default)
        => LoadBundleComponentsTreeCoreAsync(bundleId, localCache, new HashSet<Guid>(), ct);

    public async Task SyncAssembledBundlesForBundleAsync(Guid bundleId, CancellationToken ct = default)
    {
        var bundle = await db.CatalogItems
            .FirstOrDefaultAsync(x => x.Id == bundleId && x.Type == CatalogItemType.Bundle, ct)
            ?? throw new InvalidOperationException($"Bundle {bundleId} not found.");

        var cache = new Dictionary<Guid, CatalogItem>();
        var tree  = await LoadBundleComponentsTreeAsync(bundleId, cache, ct);

        var combinations = GenerateCombinations(tree);

        // Load existing assembled bundles with all includes needed for DTO mapping
        var existingAssembled = await db.CatalogItems
            .Where(x => x.SourceBundleId == bundleId)
            .Include(x => x.SourceBundle)
            .Include(x => x.Tags)
            .Include(x => x.AssembledComponents)
                .ThenInclude(c => c.Component)
                .ThenInclude(comp => comp.Group)
            .ToListAsync(ct);

        // Snapshot all existing items before any mutation — one pass, no extra queries
        var beforeSnapshots = existingAssembled.ToDictionary(
            x => x.Id,
            x => mapper.Map<CatalogItemDto>(x));

        var matchedIds = new HashSet<Guid>();
        var newItemIds = new List<Guid>();

        foreach (var combo in combinations)
        {
            var match = existingAssembled.FirstOrDefault(ab => CombinationMatches(combo, ab));

            if (match is not null)
            {
                matchedIds.Add(match.Id);
                var (name, desc) = await GenerateAssembledBundleTextsAsync(bundle.Name, combo, cache, ct);
                match.IsArchived  = false;
                match.Name        = name;
                match.Description = desc;
            }
            else
            {
                var hash          = DeterministicShortHash(combo);
                var article       = $"{bundle.Article}-{hash}";
                var (name, desc)  = await GenerateAssembledBundleTextsAsync(bundle.Name, combo, cache, ct);

                var newItem = new CatalogItem
                {
                    Id             = Guid.NewGuid(),
                    Type           = CatalogItemType.AssembledBundle,
                    SourceBundleId = bundleId,
                    Article        = article,
                    Name           = name,
                    Description    = desc,
                    IsArchived     = false,
                };
                db.CatalogItems.Add(newItem);

                foreach (var (componentId, quantity) in combo)
                {
                    db.AssembledBundleComponents.Add(new AssembledBundleComponent
                    {
                        Id                = Guid.NewGuid(),
                        AssembledBundleId = newItem.Id,
                        ComponentId       = componentId,
                        Quantity          = quantity,
                    });
                }

                newItemIds.Add(newItem.Id);
                matchedIds.Add(newItem.Id);
            }
        }

        // Archive stale assembled bundles
        foreach (var stale in existingAssembled.Where(ab => !matchedIds.Contains(ab.Id)))
            stale.IsArchived = true;

        await db.SaveChangesAsync(ct);

        // Reload all affected items for after-snapshots (one batch query)
        var allIds    = existingAssembled.Select(x => x.Id).Concat(newItemIds).ToList();
        var afterMap  = await LoadAssembledBundlesForSnapshotAsync(allIds, ct);

        // Changelog for existing (modified / archived)
        foreach (var item in existingAssembled)
        {
            var before = beforeSnapshots[item.Id];
            var after  = afterMap[item.Id];
            await changeLog.CompareAndSaveToChangelog(before, after,
                action: CatalogActions.BundleSync,
                actionData: new { BundleId = bundleId });
        }

        // Changelog for newly created
        foreach (var newId in newItemIds)
        {
            var after = afterMap[newId];
            await changeLog.CompareAndSaveToChangelog(null, after,
                action: CatalogActions.BundleSync,
                actionData: new { BundleId = bundleId });
        }
    }

    public Task SyncParentBundlesAsync(Guid componentId, CancellationToken ct = default)
        => SyncParentBundlesCoreAsync(componentId, [componentId], ct);

    public Task UpdateAssembledBundlesOnComponentChangeAsync(
        Guid componentId,
        CatalogItemDto before,
        string newName,
        string newArticle,
        string? newBarcode,
        CancellationToken ct = default)
    {
        if (before.Name == newName && before.Article == newArticle && before.Barcode == newBarcode)
            return Task.CompletedTask;

        return UpdateAssembledBundleNamesForComponentAsync(componentId, ct);
    }

    public async Task UpdateAssembledBundleNamesForComponentAsync(
        Guid changedComponentId,
        CancellationToken ct = default)
    {
        var affected = await db.CatalogItems
            .Where(x => x.Type == CatalogItemType.AssembledBundle
                     && x.AssembledComponents.Any(c => c.ComponentId == changedComponentId))
            .Include(x => x.SourceBundle)
            .Include(x => x.Tags)
            .Include(x => x.AssembledComponents)
                .ThenInclude(c => c.Component)
                .ThenInclude(comp => comp.Group)
            .ToListAsync(ct);

        if (affected.Count == 0) return;

        var beforeSnapshots = affected.ToDictionary(
            x => x.Id,
            x => mapper.Map<CatalogItemDto>(x));

        foreach (var item in affected)
        {
            var bundleName = item.SourceBundle?.Name ?? string.Empty;
            var components = item.AssembledComponents.Select(c => (c.Component, c.Quantity)).ToList();
            item.Name        = BuildAssembledBundleName(bundleName, components);
            item.Description = BuildAssembledBundleDescription(components);
        }

        await db.SaveChangesAsync(ct);

        foreach (var item in affected)
        {
            var before = beforeSnapshots[item.Id];
            var after  = mapper.Map<CatalogItemDto>(item);

            if (before.Name == after.Name) continue;

            await changeLog.CompareAndSaveToChangelog(before, after,
                action: CatalogActions.ComponentArticleChanged,
                actionData: new { ComponentId = changedComponentId });
        }
    }

    // ── Private implementation ───────────────────────────────────────────────

    private async Task SyncParentBundlesCoreAsync(
        Guid componentId,
        HashSet<Guid> visitedBundleIds,
        CancellationToken ct)
    {
        var parentBundleIds = await db.BundleComponents
            .Where(bc => bc.ComponentId == componentId)
            .Select(bc => bc.BundleId)
            .Distinct()
            .ToListAsync(ct);

        foreach (var bundleId in parentBundleIds)
        {
            if (!visitedBundleIds.Add(bundleId)) continue;
            await SyncAssembledBundlesForBundleAsync(bundleId, ct);
            await SyncParentBundlesCoreAsync(bundleId, visitedBundleIds, ct);
        }
    }

    private async Task<IReadOnlyList<BundleComponentNode>> LoadBundleComponentsTreeCoreAsync(
        Guid bundleId,
        Dictionary<Guid, CatalogItem> localCache,
        HashSet<Guid> visitedBundleIds,
        CancellationToken ct)
    {
        if (!visitedBundleIds.Add(bundleId))
            throw new BundleCircularDependencyException();

        var bundleComponents = await db.BundleComponents
            .Where(bc => bc.BundleId == bundleId)
            .Include(bc => bc.Component)
            .ToListAsync(ct);

        var nodes = new List<BundleComponentNode>();

        foreach (var bc in bundleComponents)
        {
            var component = bc.Component;
            localCache[component.Id] = component;

            BundleComponentNode node;

            switch (component.Type)
            {
                case CatalogItemType.Variation:
                {
                    var memberItems = await LoadVariationMembersAsync(component.Id, localCache, ct);
                    node = new BundleComponentNode
                    {
                        BundleComponentId = bc.Id,
                        ComponentId       = bc.ComponentId,
                        ComponentType     = component.Type,
                        Quantity          = bc.Quantity,
                        ExpandedOptions   = memberItems,
                    };
                    break;
                }
                case CatalogItemType.ProductGroup:
                {
                    var children = await LoadProductGroupChildrenAsync(component.Id, localCache, ct);
                    node = new BundleComponentNode
                    {
                        BundleComponentId = bc.Id,
                        ComponentId       = bc.ComponentId,
                        ComponentType     = component.Type,
                        Quantity          = bc.Quantity,
                        ExpandedOptions   = children,
                    };
                    break;
                }
                case CatalogItemType.Bundle:
                {
                    var nestedComponents =
                        await LoadBundleComponentsTreeCoreAsync(component.Id, localCache, visitedBundleIds, ct);
                    node = new BundleComponentNode
                    {
                        BundleComponentId = bc.Id,
                        ComponentId       = bc.ComponentId,
                        ComponentType     = component.Type,
                        Quantity          = bc.Quantity,
                        NestedComponents  = nestedComponents,
                    };
                    break;
                }
                default: // Standard / Unit — leaf
                    node = new BundleComponentNode
                    {
                        BundleComponentId = bc.Id,
                        ComponentId       = bc.ComponentId,
                        ComponentType     = component.Type,
                        Quantity          = bc.Quantity,
                    };
                    break;
            }

            nodes.Add(node);
        }

        return nodes;
    }

    private async Task<List<CatalogItem>> LoadVariationMembersAsync(
        Guid variationId,
        Dictionary<Guid, CatalogItem> cache,
        CancellationToken ct)
    {
        var memberIds = await db.CatalogItemVariationMembers
            .Where(vm => vm.VariationId == variationId)
            .Select(vm => vm.ItemId)
            .ToListAsync(ct);

        var uncachedIds = memberIds.Where(id => !cache.ContainsKey(id)).ToList();
        if (uncachedIds.Count > 0)
        {
            var loaded = await db.CatalogItems
                .Where(x => uncachedIds.Contains(x.Id))
                .ToListAsync(ct);
            foreach (var item in loaded)
                cache[item.Id] = item;
        }

        return memberIds.Select(id => cache[id]).ToList();
    }

    private async Task<List<CatalogItem>> LoadProductGroupChildrenAsync(
        Guid groupId,
        Dictionary<Guid, CatalogItem> cache,
        CancellationToken ct)
    {
        var children = await db.CatalogItems
            .Where(x => x.GroupId == groupId)
            .ToListAsync(ct);

        foreach (var child in children)
            cache[child.Id] = child;

        return children;
    }

    private static IReadOnlyList<IReadOnlyList<(Guid ComponentId, int Quantity)>> GenerateCombinations(
        IReadOnlyList<BundleComponentNode> tree)
    {
        IReadOnlyList<IReadOnlyList<(Guid, int)>> result = [[]];

        foreach (var node in tree)
        {
            var options = ExpandNode(node);
            var next    = new List<IReadOnlyList<(Guid, int)>>();

            foreach (var combo in result)
            foreach (var opt in options)
            {
                next.Add(combo.Concat(opt).ToList());
                if (next.Count > MaxCombinations)
                    throw new BundleTooManyCombinationsException(MaxCombinations);
            }

            result = next;
        }

        return result.Select(MergeDuplicateComponents).ToList();
    }

    private static IReadOnlyList<(Guid ComponentId, int Quantity)> MergeDuplicateComponents(
        IEnumerable<(Guid ComponentId, int Quantity)> combo)
        => combo
            .GroupBy(x => x.ComponentId)
            .Select(g => (g.Key, g.Sum(x => x.Quantity)))
            .ToList();

    private static IReadOnlyList<IReadOnlyList<(Guid ComponentId, int Quantity)>> ExpandNode(
        BundleComponentNode node)
    {
        switch (node.ComponentType)
        {
            case CatalogItemType.Standard:
            case CatalogItemType.Unit:
                return [[(node.ComponentId, node.Quantity)]];

            case CatalogItemType.Variation:
            case CatalogItemType.ProductGroup:
                return node.ExpandedOptions
                    .Select(opt => (IReadOnlyList<(Guid, int)>)[(opt.Id, node.Quantity)])
                    .ToList();

            case CatalogItemType.Bundle:
            {
                var subCombinations = GenerateCombinations(node.NestedComponents);
                return subCombinations
                    .Select(subCombo => (IReadOnlyList<(Guid, int)>)subCombo
                        .Select(c => (c.ComponentId, c.Quantity * node.Quantity))
                        .ToList())
                    .ToList();
            }

            default:
                return [];
        }
    }

    private static bool CombinationMatches(
        IReadOnlyList<(Guid ComponentId, int Quantity)> combination,
        CatalogItem assembledBundle)
    {
        var comboKey = combination
            .OrderBy(x => x.ComponentId)
            .Select(x => (x.ComponentId, x.Quantity))
            .ToList();

        var existingKey = assembledBundle.AssembledComponents
            .OrderBy(x => x.ComponentId)
            .Select(x => (x.ComponentId, x.Quantity))
            .ToList();

        return comboKey.SequenceEqual(existingKey);
    }

    private async Task<(string Name, string Description)> GenerateAssembledBundleTextsAsync(
        string bundleName,
        IReadOnlyList<(Guid ComponentId, int Quantity)> combination,
        Dictionary<Guid, CatalogItem> cache,
        CancellationToken ct)
    {
        var uncachedIds = combination
            .Select(x => x.ComponentId)
            .Where(id => !cache.ContainsKey(id))
            .Distinct()
            .ToList();

        if (uncachedIds.Count > 0)
        {
            var loaded = await db.CatalogItems
                .Where(x => uncachedIds.Contains(x.Id))
                .ToListAsync(ct);
            foreach (var item in loaded)
                cache[item.Id] = item;
        }

        var components = combination.Select(x => (cache[x.ComponentId], x.Quantity)).ToList();
        return (BuildAssembledBundleName(bundleName, components),
                BuildAssembledBundleDescription(components));
    }

    private static string BuildAssembledBundleName(
        string bundleName,
        IEnumerable<(CatalogItem Component, int Quantity)> components)
    {
        var parts = components
            .OrderBy(c => c.Component.Article)
            .Select(c => $"{c.Quantity}x {c.Component.Article}");

        return $"{bundleName} [{string.Join(" + ", parts)}]";
    }

    private static string BuildAssembledBundleDescription(
        IEnumerable<(CatalogItem Component, int Quantity)> components)
    {
        var parts = components
            .OrderBy(c => c.Component.Article)
            .Select(c =>
            {
                var id = string.IsNullOrEmpty(c.Component.Barcode)
                    ? c.Component.Article
                    : $"{c.Component.Article}, {c.Component.Barcode}";
                return $"{c.Component.Name} ({id})";
            });

        return string.Join(", ", parts);
    }

    private static string DeterministicShortHash(IReadOnlyList<(Guid ComponentId, int Quantity)> combo)
    {
        var bytes = combo
            .OrderBy(x => x.ComponentId)
            .SelectMany(x => x.ComponentId.ToByteArray().Concat(BitConverter.GetBytes(x.Quantity)))
            .ToArray();

        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash)[..16].ToLower();
    }

    private async Task<Dictionary<Guid, CatalogItemDto>> LoadAssembledBundlesForSnapshotAsync(
        IReadOnlyList<Guid> ids,
        CancellationToken ct)
    {
        var items = await db.CatalogItems
            .Where(x => ids.Contains(x.Id))
            .Include(x => x.SourceBundle)
            .Include(x => x.Tags)
            .Include(x => x.AssembledComponents)
                .ThenInclude(c => c.Component)
                .ThenInclude(comp => comp.Group)
            .ToListAsync(ct);

        return items.ToDictionary(x => x.Id, x => mapper.Map<CatalogItemDto>(x));
    }
}
