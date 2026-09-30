using ProjectWarehouse.Server.Domain;
using ProjectWarehouse.Server.Infrastructure;
using ProjectWarehouse.Server.Infrastructure.ChangeLog;
using ProjectWarehouse.Server.Models;
using ProjectWarehouse.Server.Models.Tags;

namespace ProjectWarehouse.Server.Services;

/// <summary>How a module snapshots its documents for the changelog around a batch change.</summary>
public record DocumentBatchChangelog<TDoc, TDto>(
    IChangeLogService<TDto> Service,
    Func<TDoc, Dictionary<Guid, StoragePlaceNode>, TDto> Map);

/// <summary>The module's 404 for a batch: its code, the arg listing the numbers and a plural noun for the message.</summary>
public record DocumentBatchNotFound(ErrorCode Code, string NumbersArg, string Noun);

/// <summary>The batch endpoints every warehouse document module shares — the module supplies access and mapping.</summary>
public interface IDocumentBatchService
{
    /// <summary>
    /// Runs <paramref name="transition"/> over each document on its own, in request order, and logs the ones that
    /// went through. <paramref name="editable"/> carries the caller's edit access and the Include chain the
    /// transition and the snapshot need; an id it does not yield fails with <paramref name="notFound"/>.
    /// </summary>
    Task<DocumentBatchTransitionResponse> TransitionAsync<TDoc, TDto>(
        IQueryable<TDoc> editable,
        IReadOnlyList<Guid> ids,
        Func<TDoc, Task<AppProblemDetails?>> transition,
        DocumentBatchChangelog<TDoc, TDto> changelog,
        string action,
        AppProblemDetails notFound,
        CancellationToken ct = default)
        where TDoc : class, IWarehouseDocument;

    /// <summary>
    /// Adds or removes one tag on every document, all or nothing. Null on success, otherwise 422
    /// <c>tagNotFound</c> or the module's 404 naming the blocking documents the caller can see in
    /// <paramref name="viewable"/>.
    /// </summary>
    Task<AppProblemDetails?> UpdateTagsAsync<TDoc, TTag, TDto>(
        IQueryable<TDoc> editable,
        IQueryable<TDoc> viewable,
        IQueryable<TTag> tags,
        BatchUpdateTagsRequest request,
        DocumentBatchChangelog<TDoc, TDto> changelog,
        DocumentBatchNotFound notFound,
        CancellationToken ct = default)
        where TDoc : class, ITaggedWarehouseDocument<TTag>
        where TTag : Tag;
}
