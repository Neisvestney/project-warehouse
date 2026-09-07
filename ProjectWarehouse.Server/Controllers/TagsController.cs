using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProjectWarehouse.Server.Domain;
using ProjectWarehouse.Server.Infrastructure;
using ProjectWarehouse.Server.Infrastructure.ChangeLog;
using ProjectWarehouse.Server.Infrastructure.Realtime;
using ProjectWarehouse.Server.Models;
using ProjectWarehouse.Server.Models.Tags;
using ProjectWarehouse.Server.Services;

namespace ProjectWarehouse.Server.Controllers;

/// <summary>
/// Tag administration across every kind. The per-module endpoints (<c>/api/receipts/tags</c>,
/// <c>/api/catalog/tags</c>) stay where they are — they serve the pickers inside those forms and create a tag
/// under that module's own Edit permission. Renaming and deleting affect everyone, and live here.
/// </summary>
[Route("api/tags")]
public class TagsController(
    ITagsService tags,
    IRealtimeNotifier realtime,
    IChangeLogService<TagDto> changeLog) : AppControllerBase
{
    /// <summary>The tag list is watched as one object — the event is keyed by an empty id.</summary>
    private static readonly Guid TagsEntityId = Guid.Empty;

    /// <summary>All tags with the number of objects bound to each.</summary>
    /// <remarks>
    /// Query params: <c>kind</c> (optional — every kind when omitted), <c>search</c> (optional). Not
    /// paginated; ordered by kind, then by name. Requires <c>tags.manage</c>; 403 <c>permissionDenied</c>
    /// otherwise.
    /// </remarks>
    [HttpGet]
    [Authorize(Policy = Permissions.Tags.Manage)]
    [ProducesResponseType<IReadOnlyList<TagDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(
        [FromQuery] TagKind? kind = null,
        [FromQuery] string? search = null,
        CancellationToken ct = default)
    {
        return Ok(await tags.GetAllAsync(kind, search, ct));
    }

    /// <summary>Create a tag of the given kind.</summary>
    /// <remarks>
    /// Body: <c>CreateTagRequest</c> — kind and name (trimmed before saving). Errors: 422
    /// <c>required</c> (field <c>name</c>) when the trimmed name is empty; 422 <c>tagNameDuplicate</c>
    /// (field <c>name</c>) when a tag of the same kind already carries the name. Requires <c>tags.manage</c>.
    /// </remarks>
    [HttpPost]
    [Authorize(Policy = Permissions.Tags.Manage)]
    [ProducesResponseType<TagDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Create([FromBody] CreateTagRequest request, CancellationToken ct = default)
    {
        var name = request.Name.Trim();
        if (name.Length == 0)
            return UnprocessableEntity("name", ErrorCode.Required, "Tag name cannot be blank.");

        if (await tags.NameTakenAsync(request.Kind, name, ct: ct))
            return DuplicateName(name);

        TagDto dto;
        try
        {
            dto = await tags.CreateAsync(request.Kind, name, ct);
        }
        catch (DbUpdateException e) when (UniqueViolations.IsTagName(e))
        {
            return DuplicateName(name);
        }

        await changeLog.CompareAndSaveToChangelog(null, dto);
        await PublishTagsChangedAsync(ct);

        return Created($"/api/tags/{dto.Id}", dto);
    }

    /// <summary>Rename a tag. Everything bound to it keeps its binding.</summary>
    /// <remarks>
    /// Body: <c>RenameTagRequest</c> — name (trimmed before saving). Errors: 404 <c>tagNotFound</c>; 422
    /// <c>required</c> (field <c>name</c>) when the trimmed name is empty; 422 <c>tagNameDuplicate</c>
    /// (field <c>name</c>) when another tag of the same kind already carries the name. Requires
    /// <c>tags.manage</c>.
    /// </remarks>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = Permissions.Tags.Manage)]
    [ProducesResponseType<TagDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Rename(Guid id, [FromBody] RenameTagRequest request, CancellationToken ct = default)
    {
        var name = request.Name.Trim();
        if (name.Length == 0)
            return UnprocessableEntity("name", ErrorCode.Required, "Tag name cannot be blank.");

        var before = await tags.FindAsync(id, ct);
        if (before is null)
            return NotFound(ErrorCode.TagNotFound, "Tag not found.");

        if (await tags.NameTakenAsync(before.Kind, name, id, ct))
            return DuplicateName(name);

        TagDto? after;
        try
        {
            after = await tags.RenameAsync(id, name, ct);
        }
        catch (DbUpdateException e) when (UniqueViolations.IsTagName(e))
        {
            return DuplicateName(name);
        }

        if (after is null)
            return NotFound(ErrorCode.TagNotFound, "Tag not found.");

        await changeLog.CompareAndSaveToChangelog(before, after);
        await PublishTagsChangedAsync(ct);

        return Ok(after);
    }

    /// <summary>Delete a tag and unbind it from every object carrying it.</summary>
    /// <remarks>
    /// The objects themselves are untouched — they simply lose the tag. Read <c>usageCount</c> from the list
    /// endpoint first if the caller should be warned about how many. Errors: 404 <c>tagNotFound</c>. Requires
    /// <c>tags.manage</c>.
    /// </remarks>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = Permissions.Tags.Manage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct = default)
    {
        var deleted = await tags.DeleteAsync(id, ct);
        if (deleted is null)
            return NotFound(ErrorCode.TagNotFound, "Tag not found.");

        await changeLog.CompareAndSaveToChangelog(deleted, null);
        await PublishTagsChangedAsync(ct);

        return NoContent();
    }

    private ObjectResult DuplicateName(string name) =>
        UnprocessableEntity("name", ErrorCode.TagNameDuplicate, $"A tag named '{name}' already exists.");

    // The changelog fires on a single tag; the settings page watches the list, so it needs waking explicitly.
    private ValueTask PublishTagsChangedAsync(CancellationToken ct) =>
        realtime.PublishEntityChangedAsync(AppEntityType.Tags, TagsEntityId, HttpContext, ct);
}
