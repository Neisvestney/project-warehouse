using Microsoft.EntityFrameworkCore;
using ProjectWarehouse.Server.Data;
using ProjectWarehouse.Server.Domain;
using ProjectWarehouse.Server.Infrastructure;
using ProjectWarehouse.Server.Models;
using ProjectWarehouse.Server.Models.Tags;

namespace ProjectWarehouse.Server.Services;

public class DocumentBatchService(ApplicationDbContext db) : IDocumentBatchService
{
    public async Task<DocumentBatchTransitionResponse> TransitionAsync<TDoc, TDto>(
        IQueryable<TDoc> editable,
        IReadOnlyList<Guid> ids,
        Func<TDoc, Task<AppProblemDetails?>> transition,
        DocumentBatchChangelog<TDoc, TDto> changelog,
        string action,
        AppProblemDetails notFound,
        CancellationToken ct = default)
        where TDoc : class, IWarehouseDocument
    {
        var distinctIds = ids.Distinct().ToList();
        var loaded      = await LoadAsync(editable, distinctIds, ct);
        var nodeById    = await LoadWarehouseNodesAsync(loaded.Values, ct);

        var transitionedIds = new List<Guid>();
        var failedItems     = new List<DocumentBatchTransitionFailedItem>();

        foreach (var id in distinctIds)
        {
            if (!loaded.TryGetValue(id, out var document))
            {
                failedItems.Add(DocumentBatchTransitionFailedItem.From(id, null, notFound));
                continue;
            }

            var before = changelog.Map(document, nodeById);
            if (await transition(document) is { } problem)
            {
                failedItems.Add(DocumentBatchTransitionFailedItem.From(id, document.Number, problem));
                continue;
            }

            transitionedIds.Add(id);
            await changelog.Service.CompareAndSaveToChangelog(before, changelog.Map(document, nodeById), action);
        }

        return new DocumentBatchTransitionResponse
        {
            TransitionedIds = transitionedIds,
            FailedItems     = failedItems,
        };
    }

    public async Task<AppProblemDetails?> UpdateTagsAsync<TDoc, TTag, TDto>(
        IQueryable<TDoc> editable,
        IQueryable<TDoc> viewable,
        IQueryable<TTag> tags,
        BatchUpdateTagsRequest request,
        DocumentBatchChangelog<TDoc, TDto> changelog,
        DocumentBatchNotFound notFound,
        CancellationToken ct = default)
        where TDoc : class, ITaggedWarehouseDocument<TTag>
        where TTag : Tag
    {
        var tag = await tags.FirstOrDefaultAsync(t => t.Id == request.TagId, ct);
        if (tag is null)
            return AppProblems.UnprocessableEntity("tagId", ErrorCode.TagNotFound, "Tag not found.");

        var ids    = request.Ids.Distinct().ToList();
        var loaded = await LoadAsync(editable, ids, ct);

        if (loaded.Count != ids.Count)
        {
            var missingIds = ids.Where(id => !loaded.ContainsKey(id)).ToList();
            // only the numbers the caller may see: an id outside view access must not reveal its document
            var missingNumbers = await viewable
                .Where(d => missingIds.Contains(d.Id))
                .OrderBy(d => d.Number)
                .Select(d => d.Number)
                .ToListAsync(ct);

            return AppProblems.Root(StatusCodes.Status404NotFound, notFound.Code,
                $"{missingIds.Count} of {ids.Count} {notFound.Noun} were not found or cannot be edited.",
                new Dictionary<string, object>
                {
                    [notFound.NumbersArg] = missingNumbers,
                    ["count"]             = missingIds.Count,
                });
        }

        var nodeById = await LoadWarehouseNodesAsync(loaded.Values, ct);
        var changes  = new List<(TDoc document, TDto before)>();

        foreach (var document in loaded.Values)
        {
            var existing = document.Tags.FirstOrDefault(t => t.Id == tag.Id);
            if ((existing is null) != (request.Operation == TagBatchOperation.Add))
                continue;

            changes.Add((document, changelog.Map(document, nodeById)));

            if (existing is null)
                document.Tags.Add(tag);
            else
                document.Tags.Remove(existing);
        }

        await db.SaveChangesAsync(ct);

        foreach (var (document, before) in changes)
            await changelog.Service.CompareAndSaveToChangelog(before, changelog.Map(document, nodeById));

        return null;
    }

    private static Task<Dictionary<Guid, TDoc>> LoadAsync<TDoc>(
        IQueryable<TDoc> source, IReadOnlyCollection<Guid> ids, CancellationToken ct)
        where TDoc : class, IWarehouseDocument =>
        source.Where(d => ids.Contains(d.Id)).ToDictionaryAsync(d => d.Id, ct);

    private async Task<Dictionary<Guid, StoragePlaceNode>> LoadWarehouseNodesAsync(
        IEnumerable<IWarehouseDocument> documents, CancellationToken ct)
    {
        var warehouseIds = documents.Select(d => d.WarehouseId).Distinct().ToList();
        if (warehouseIds.Count == 0)
            return [];

        return await db.StoragePlacesNodes
            .Where(n => warehouseIds.Contains(n.RootStoragePlace.WarehouseId))
            .Include(n => n.RootStoragePlace)
            .ToDictionaryAsync(n => n.Id, ct);
    }
}
