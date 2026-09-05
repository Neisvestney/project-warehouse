using System.ComponentModel.DataAnnotations;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProjectWarehouse.Server.Data;
using ProjectWarehouse.Server.Domain;
using ProjectWarehouse.Server.Infrastructure;
using ProjectWarehouse.Server.Infrastructure.ChangeLog;
using ProjectWarehouse.Server.Models;
using ProjectWarehouse.Server.Models.Catalog;
using ProjectWarehouse.Server.Services;

namespace ProjectWarehouse.Server.Controllers;

[Route("api/catalog")]
public class CatalogController(
    ApplicationDbContext db,
    IMapper mapper,
    IChangeLogService<CatalogItemDto> changeLog,
    ICatalogService catalogService,
    IDataFileBindingService fileBinding) : AppControllerBase
{
    /// <summary>List all catalog item tags, optionally filtered by name.</summary>
    /// <remarks>
    /// Query params: <c>search</c> (optional). Not paginated — ordered by name.
    /// Requires <c>catalog.view</c>. No error codes beyond 403 <c>permissionDenied</c>.
    /// </remarks>
    [HttpGet("tags")]
    [Authorize(Policy = Permissions.Catalog.View)]
    [ProducesResponseType<IReadOnlyList<CatalogItemTagDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTags([FromQuery] string? search = null, CancellationToken ct = default)
    {
        var tags = await db.CatalogItemTags
            .WhereMatchesSearch(t => t.SearchString, search)
            .OrderBy(t => t.Name)
            .Select(t => new CatalogItemTagDto { Id = t.Id, Name = t.Name })
            .ToListAsync(ct);

        return Ok(tags);
    }

    /// <summary>Create a new catalog item tag.</summary>
    /// <remarks>
    /// Requires <c>catalog.edit</c>. Body: <c>CreateCatalogItemTagRequest</c> — name (trimmed before saving).
    /// Errors: 422 <c>validationError</c> (field <c>name</c>) when the trimmed name is empty; 422
    /// <c>tagNameDuplicate</c> (field <c>name</c>) when another catalog item tag already has this name; 403
    /// <c>permissionDenied</c>.
    /// </remarks>
    [HttpPost("tags")]
    [Authorize(Policy = Permissions.Catalog.Edit)]
    [ProducesResponseType<CatalogItemTagDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> CreateTag([FromBody] CreateCatalogItemTagRequest request, CancellationToken ct = default)
    {
        var name = request.Name.Trim();
        if (name.Length == 0)
            return UnprocessableEntity("name", ErrorCode.ValidationError, "Tag name cannot be blank.");

        var duplicate = await db.CatalogItemTags.AnyAsync(t => t.Name == name, ct);
        if (duplicate)
            return UnprocessableEntity("name", ErrorCode.TagNameDuplicate, $"A tag named '{name}' already exists.");

        var tag = new CatalogItemTag { Id = Guid.NewGuid(), Name = name };
        db.CatalogItemTags.Add(tag);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException e) when (UniqueViolations.IsTagName(e))
        {
            return UnprocessableEntity("name", ErrorCode.TagNameDuplicate, $"A tag named '{name}' already exists.");
        }

        var dto = new CatalogItemTagDto { Id = tag.Id, Name = tag.Name };
        return Created($"/api/catalog/tags/{tag.Id}", dto);
    }

    /// <summary>List all catalog items (paginated, optionally filtered by name).</summary>
    /// <remarks>
    /// Query params: <c>page</c> (default 1), <c>pageSize</c> (default 20, max 200), <c>searchString</c>,
    /// <c>sortBy</c> (default <c>Name</c>), <c>sortOrder</c> (default <c>Asc</c>), <c>itemTypes</c>,
    /// <c>tagIds</c>, <c>isArchived</c>. Archived items always sort last, whatever <c>sortBy</c> says.
    /// Product-group children (<c>groupId != null</c>) are excluded — this is the catalog tree, and a child is
    /// reached through its group. Use <see cref="GetForSelect"/> when children must be pickable.
    /// Requires <c>catalog.view</c>. No error codes beyond 403 <c>permissionDenied</c>.
    /// </remarks>
    [HttpGet]
    [Authorize(Policy = Permissions.Catalog.View)]
    [ProducesResponseType<Paginated<CatalogItemSummaryDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(
        [FromQuery][Range(1, int.MaxValue)] int page = 1,
        [FromQuery][Range(1, 200)] int pageSize = 20,
        [FromQuery] string? searchString = null,
        [FromQuery] CatalogSortBy sortBy = CatalogSortBy.Name,
        [FromQuery] SortOrder sortOrder = SortOrder.Asc,
        [FromQuery] IReadOnlyList<CatalogItemType>? itemTypes = null,
        [FromQuery] IReadOnlyList<Guid>? tagIds = null,
        [FromQuery] bool? isArchived = null,
        CancellationToken ct = default)
    {
        var baseQuery = db.CatalogItems
            .Where(c => c.GroupId == null)
            .WhereMatchesSearch(c => c.SearchString, searchString);

        if (itemTypes != null && itemTypes.Count > 0)
        {
            baseQuery = baseQuery.Where(c => itemTypes.Contains(c.Type));
        }

        if (tagIds != null && tagIds.Count > 0)
        {
            baseQuery = baseQuery.Where(c => c.Tags.Any(t => tagIds.Contains(t.Id)));
        }

        if (isArchived != null)
        {
            baseQuery = baseQuery.Where(c => c.IsArchived == isArchived.Value);
        }
            
        var orderedQuery = baseQuery
            .OrderBy(c => c.IsArchived);

        var query = sortBy switch
        {
            CatalogSortBy.Article => orderedQuery.ThenSort(c => c.Article, sortOrder).ThenBy(c => c.Id),
            CatalogSortBy.Barcode => orderedQuery.ThenSort(c => c.Barcode, sortOrder).ThenBy(c => c.Id),
            CatalogSortBy.Type    => orderedQuery.ThenSort(c => c.Type, sortOrder).ThenBy(c => c.Id),
            _                     => orderedQuery.ThenSort(c => c.Name, sortOrder).ThenBy(c => c.Id),
        };

        var paginated = await query
            .ProjectTo<CatalogItemSummaryDto>(mapper.ConfigurationProvider)
            .ToPaginatedAsync(page, pageSize, ct);

        return Ok(paginated);
    }

    /// <summary>Get a flat list of catalog items for use in select/autocomplete controls.</summary>
    /// <remarks>
    /// Query params: <c>searchString</c>, <c>types</c>, <c>tagIds</c>, <c>take</c> (default 10, max 200).
    /// Unlike <see cref="GetAll"/>, product-group children are included — a picker must be able to reach them.
    /// Archived items are returned too, sorted last.
    /// Requires <c>catalog.view</c>. No error codes beyond 403 <c>permissionDenied</c>.
    /// </remarks>
    [HttpGet("for-select")]
    [Authorize(Policy = Permissions.Catalog.View)]
    [ProducesResponseType<IReadOnlyList<CatalogItemSelectDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetForSelect(
        [FromQuery] string? searchString = null,
        [FromQuery] IReadOnlyList<CatalogItemType>? types = null,
        [FromQuery] IReadOnlyList<Guid>? tagIds = null,
        [FromQuery][Range(1, 200)] int take = 10,
        CancellationToken ct = default)
    {
        var query = db.CatalogItems
            .WhereMatchesSearch(c => c.SearchString, searchString);

        if (types != null && types.Count > 0)
            query = query.Where(c => types.Contains(c.Type));

        if (tagIds != null && tagIds.Count > 0)
            query = query.Where(c => c.Tags.Any(t => tagIds.Contains(t.Id)));

        var items = await query
            .OrderBy(c => c.IsArchived)
            .ThenBy(c => c.Name)
            .ThenBy(c => c.Id)
            .Take(take)
            .ProjectTo<CatalogItemSelectDto>(mapper.ConfigurationProvider)
            .ToListAsync(ct);

        return Ok(items);
    }

    /// <summary>Get a catalog item by ID.</summary>
    /// <remarks>
    /// Requires <c>catalog.view</c>. Works for product-group children as well as top-level items.
    /// Returns 404 <c>catalogItemNotFound</c> if the item does not exist.
    /// </remarks>
    [HttpGet("{id:guid}")]
    [Authorize(Policy = Permissions.Catalog.View)]
    [ProducesResponseType<CatalogItemDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct = default)
    {
        var item = await LoadItemWithDetailsAsync(id, ct);

        if (item is null)
            return NotFound(ErrorCode.CatalogItemNotFound, "Catalog item not found.");

        return Ok(mapper.Map<CatalogItemDto>(item));
    }

    /// <summary>Create a new catalog item.</summary>
    /// <remarks>
    /// Requires <c>catalog.edit</c>. Body: <c>CreateCatalogItemRequest</c> — type, name, article, barcode and
    /// an optional <c>mainImageFileId</c>. <c>type</c> is fixed at creation: it can never be changed later
    /// (<c>catalogItemIsImmutable</c>). Type-specific structure — group, variations, components, children —
    /// is set through <c>PUT /api/catalog/{id}</c>.
    /// Error codes:
    /// <list type="bullet">
    ///   <item>422 <c>catalogItemArticleDuplicate</c> (field <c>article</c>) — another item already has this article</item>
    ///   <item>422 <c>catalogItemBarcodeDuplicate</c> (field <c>barcode</c>) — another item already has this barcode</item>
    ///   <item>422 <c>dataFileNotFound</c> (field <c>mainImageFileId</c>) — the uploaded file was collected before the form was saved</item>
    /// </list>
    /// </remarks>
    [HttpPost]
    [Authorize(Policy = Permissions.Catalog.Edit)]
    [ProducesResponseType<CatalogItemDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Create([FromBody] CreateCatalogItemRequest request, CancellationToken ct = default)
    {
        var duplicateErrors = await ValidateDuplicates(request.Article, request.Barcode, excludeItemId: null, ct);
        if (duplicateErrors.Count > 0)
            return Problem(AppProblems.UnprocessableEntities(duplicateErrors));

        var item = new CatalogItem
        {
            Id = Guid.NewGuid(),
            Type = request.Type,
            Name = request.Name,
            Article = request.Article,
            Barcode = request.Barcode,
        };

        var imageProblem = await fileBinding.BindSingleAsync(
            request.MainImageFileId, v => item.MainImageFileId = v, "mainImageFileId", ct);
        if (imageProblem is not null) return Problem(imageProblem);

        db.CatalogItems.Add(item);
        await db.SaveChangesAsync(ct);

        var created = await LoadItemWithDetailsAsync(item.Id, ct);
        var dto = mapper.Map<CatalogItemDto>(created!);
        await changeLog.CompareAndSaveToChangelog(null, dto);

        return CreatedAtAction(nameof(GetById), new { id = item.Id }, dto);
    }

    /// <summary>Update a catalog item.</summary>
    /// <remarks>
    /// Requires <c>catalog.edit</c>. Type-specific fields:
    /// <list type="bullet">
    ///   <item><b>Standard / Unit</b>: <c>groupId</c>, <c>variationIds</c> (full replace)</item>
    ///   <item><b>Variation</b>: <c>memberIds</c> (full replace)</item>
    ///   <item><b>Bundle</b>: <c>components</c> — <c>id: null</c> creates, <c>id</c> present updates, missing existing entries are deleted</item>
    ///   <item><b>ProductGroup</b>: <c>children</c> — full-replace sync: <c>id: null</c> creates a child,
    ///         <c>id</c> present updates it, and a child omitted from the list is <b>deleted</b>. Children
    ///         inherit the group's tags and <c>isArchived</c>, and may only be Standard or Unit</item>
    /// </list>
    /// Images: <c>mainImageFileId</c> plus <c>images[{ id, fileId, order }]</c> — the list is a full replace
    /// (<c>id: null</c> adds a link, an omitted link is removed); the same pair applies per product-group child.
    /// Error codes:
    /// <list type="bullet">
    ///   <item>404 <c>catalogItemNotFound</c> — no such item</item>
    ///   <item>422 <c>catalogItemManagedByGroup</c> (field <c>root</c>) — the item is a product-group child; edit it through its group</item>
    ///   <item>422 <c>catalogItemArticleDuplicate</c> / <c>catalogItemBarcodeDuplicate</c> (fields <c>article</c>, <c>barcode</c>,
    ///         or <c>children[i].article</c> / <c>children[i].barcode</c>) — collides with another item, or with another entry of the same request</item>
    ///   <item>422 <c>catalogItemGroupInvalid</c> — <c>groupId</c> is not an existing ProductGroup, or a
    ///         <c>children[i].type</c> is neither Standard nor Unit</item>
    ///   <item>422 <c>catalogItemVariationInvalid</c> (field <c>memberIds[i]</c>) — the member does not exist or is not Standard/Unit/Bundle</item>
    ///   <item>422 <c>catalogItemComponentInvalid</c> (field <c>components[i].componentId</c>) — the component does not exist
    ///         or its type may not be used as a bundle component</item>
    ///   <item>422 <c>catalogItemComponentNotFound</c> (field <c>components[i].id</c>) — the component row does not belong to this bundle</item>
    ///   <item>422 <c>catalogItemNotFound</c> (field <c>children[i].id</c>) — the child does not belong to this product group</item>
    ///   <item>422 <c>catalogItemIsImmutable</c> (field <c>children[i].type</c>) — a child's type cannot be changed</item>
    ///   <item>422 <c>catalogItemCircularDependency</c> (field <c>root</c>) — the submitted components/members would
    ///         close a cycle in the Bundle↔Variation nesting graph</item>
    ///   <item>422 <c>dataFileNotFound</c> (fields <c>mainImageFileId</c>, <c>images</c>,
    ///         <c>children.mainImageFileId</c>, <c>children.images</c>) — a referenced upload no longer exists</item>
    ///   <item>409 <c>catalogItemIsInUse</c> — a child removed from <c>children</c> is still stored in a warehouse</item>
    /// </list>
    /// </remarks>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = Permissions.Catalog.Edit)]
    [ProducesResponseType<CatalogItemDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateCatalogItemRequest request, CancellationToken ct = default)
    {
        var item = await db.CatalogItems
            .AsSplitQuery()
            .Include(c => c.Group)
            .Include(c => c.Tags)
            .Include(c => c.BundleComponents).ThenInclude(bc => bc.Component)
            .Include(c => c.VariationMemberships)
            .Include(c => c.VariationMembers)
            .Include(c => c.Images)
            .FirstOrDefaultAsync(c => c.Id == id, ct);

        if (item is null)
            return NotFound(ErrorCode.CatalogItemNotFound, "Catalog item not found.");

        if (item.GroupId is not null)
            return UnprocessableEntity("root", ErrorCode.CatalogItemManagedByGroup,
                "Items belonging to a product group must be edited via the product group page.");

        var duplicateErrors = await ValidateDuplicates(request.Article, request.Barcode, excludeItemId: id, ct);
        if (duplicateErrors.Count > 0)
            return Problem(AppProblems.UnprocessableEntities(duplicateErrors));

        var beforeDto = mapper.Map<CatalogItemDto>(item);

        item.Name = request.Name;
        item.Article = request.Article;
        item.Barcode = request.Barcode;
        item.Description = request.Description;
        item.Notes = request.Notes;
        item.LabelText = request.LabelText;
        item.IsArchived = request.IsArchived;

        var newTags = await db.CatalogItemTags.Where(t => request.Tags.Contains(t.Id)).ToListAsync(ct);
        item.Tags.Clear();
        foreach (var tag in newTags)
            item.Tags.Add(tag);

        var imageProblem =
            await fileBinding.BindSingleAsync(request.MainImageFileId,
                v => item.MainImageFileId = v, "mainImageFileId", ct)
            ?? await fileBinding.BindListAsync(request.Images, item.Images, db.CatalogItemImages,
                setOwner: img => img.CatalogItemId = item.Id, field: "images", ct);
        if (imageProblem is not null) return Problem(imageProblem);

        switch (item.Type)
        {
            case CatalogItemType.Standard:
            case CatalogItemType.Unit:
            {
                var err = await ValidateGroupId(request.GroupId, ct);
                if (err is not null) return err;
                item.GroupId = request.GroupId;

                break;
            }
            case CatalogItemType.Variation:
            {
                var err = await ValidateMemberIds(request.MemberIds, "memberIds", ct);
                if (err is not null) return err;
                SyncVariationMembers(item, request.MemberIds, id);
                break;
            }
            case CatalogItemType.Bundle:
            {
                var err = await ValidateBundleComponentIds(request.Components, ct);
                if (err is not null) return err;
                err = await ValidateExistingBundleComponentIds(request.Components, id, ct);
                if (err is not null) return err;
                SyncBundleComponents(item, request.Components, id);
                break;
            }
            case CatalogItemType.ProductGroup:
            {
                var err = await SyncGroupChildren(request.Children, id, request.Tags, request.IsArchived, ct);
                if (err is not null) return err;
                break;
            }
        }

        var cycleErr = await EnsureNoBundleOrVariationCycle(item, request, ct);
        if (cycleErr is not null) return cycleErr;

        await db.SaveChangesAsync(ct);

        var afterItem = await LoadItemWithDetailsAsync(id, ct);
        var afterDto = mapper.Map<CatalogItemDto>(afterItem!);
        await changeLog.CompareAndSaveToChangelog(beforeDto, afterDto);

        return Ok(afterDto);
    }

    /// <summary>
    /// For Bundle/Variation saves, checks the submitted components/members for a circular
    /// dependency in the Bundle↔Variation nesting graph. Returns a 422 result on a cycle,
    /// otherwise null.
    /// </summary>
    private async Task<IActionResult?> EnsureNoBundleOrVariationCycle(
        CatalogItem item, UpdateCatalogItemRequest request, CancellationToken ct)
    {
        if (item.Type != CatalogItemType.Bundle && item.Type != CatalogItemType.Variation)
            return null;

        var edgeIds = item.Type == CatalogItemType.Bundle
            ? request.Components.Select(c => c.ComponentId).ToList()
            : request.MemberIds.ToList();

        try
        {
            await catalogService.EnsureNoCycleAsync(item.Id, item.Type, edgeIds, ct);
            return null;
        }
        catch (BundleCircularDependencyException)
        {
            return UnprocessableEntity("root", ErrorCode.CatalogItemCircularDependency,
                "Circular dependency detected in bundle components.");
        }
    }

    /// <summary>Delete a catalog item.</summary>
    /// <remarks>
    /// Requires <c>catalog.edit</c>. Deleting a ProductGroup deletes its children with it, and the in-use
    /// check covers them too.
    /// Returns 404 <c>catalogItemNotFound</c> if no such item, and 409 <c>catalogItemIsInUse</c> if the item
    /// (or one of its group children) is stored in any warehouse — as a node item group or as a unit
    /// inventory item.
    /// </remarks>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = Permissions.Catalog.Edit)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct = default)
    {
        var item = await db.CatalogItems
            .Include(c => c.Group)
            .Include(c => c.GroupChildren)
            .FirstOrDefaultAsync(c => c.Id == id, ct);

        if (item is null)
            return NotFound(ErrorCode.CatalogItemNotFound, "Catalog item not found.");

        var allIds = item.GroupChildren.Select(c => c.Id).Append(id).ToList();

        var isInItemsGroup = await db.StoragePlacesNodesItemsGroups
            .AnyAsync(g => allIds.Contains(g.CatalogItemId) && g.Count > 0, ct);
        var isInInventory = await db.InventoryItems
            .AnyAsync(i => allIds.Contains(i.CatalogItemId), ct);

        if (isInItemsGroup || isInInventory)
            return Conflict(ErrorCode.CatalogItemIsInUse, "Cannot delete a catalog item that is stored in a warehouse.");

        var itemForLog = await LoadItemWithDetailsAsync(id, ct);
        var dto = mapper.Map<CatalogItemDto>(itemForLog!);

        foreach (var child in item.GroupChildren)
            db.CatalogItems.Remove(child);

        db.CatalogItems.Remove(item);
        await db.SaveChangesAsync(ct);

        await changeLog.CompareAndSaveToChangelog(dto, null);

        return NoContent();
    }

    private async Task<AppProblemDetails?> BindChildImagesAsync(
        ProductGroupChildRequest request, CatalogItem child, CancellationToken ct) =>
        await fileBinding.BindSingleAsync(request.MainImageFileId,
            v => child.MainImageFileId = v, "children.mainImageFileId", ct)
        ?? await fileBinding.BindListAsync(request.Images, child.Images, db.CatalogItemImages,
            setOwner: img => img.CatalogItemId = child.Id, field: "children.images", ct);

    // nine collection includes in one query multiply into each other; images pushed it over the edge
    private Task<CatalogItem?> LoadItemWithDetailsAsync(Guid id, CancellationToken ct) =>
        db.CatalogItems
            .AsSplitQuery()
            .Include(c => c.Group).ThenInclude(g => g!.MainImageFile).ThenInclude(f => f!.CreatedBy)
            .Include(c => c.Tags)
            .Include(c => c.BundleComponents).ThenInclude(bc => bc.Component).ThenInclude(comp => comp.Group)
            .Include(c => c.VariationMemberships)
            .Include(c => c.VariationMembers)
            .Include(c => c.MainImageFile).ThenInclude(f => f!.CreatedBy)
            .Include(c => c.Images).ThenInclude(i => i.DataFile).ThenInclude(f => f.CreatedBy)
            .Include(c => c.GroupChildren).ThenInclude(child => child.Tags)
            .Include(c => c.GroupChildren).ThenInclude(child => child.VariationMemberships)
            .Include(c => c.GroupChildren).ThenInclude(child => child.MainImageFile).ThenInclude(f => f!.CreatedBy)
            .Include(c => c.GroupChildren).ThenInclude(child => child.Images).ThenInclude(i => i.DataFile)
            .Include(c => c.MarketplaceCards).ThenInclude(child => child.MarketplaceAccount)
            .FirstOrDefaultAsync(c => c.Id == id, ct);

    private async Task<IActionResult?> ValidateGroupId(Guid? groupId, CancellationToken ct)
    {
        if (groupId is null) return null;
        var valid = await db.CatalogItems
            .AnyAsync(c => c.Id == groupId && c.Type == CatalogItemType.ProductGroup, ct);
        return valid
            ? null
            : UnprocessableEntity("groupId", ErrorCode.CatalogItemGroupInvalid,
                "The specified group does not exist or is not a product group.");
    }

    private async Task<IActionResult?> ValidateVariationIds(IReadOnlyList<Guid> variationIds, string field, CancellationToken ct)
    {
        if (variationIds.Count == 0) return null;
        var validIdSet = (await db.CatalogItems
            .Where(c => variationIds.Contains(c.Id) && c.Type == CatalogItemType.Variation)
            .Select(c => c.Id)
            .ToListAsync(ct)).ToHashSet();
        var errors = variationIds
            .Select((id, i) => (id, i))
            .Where(x => !validIdSet.Contains(x.id))
            .Select(x => (Field: $"{field}[{x.i}]", Code: ErrorCode.CatalogItemVariationInvalid,
                Message: $"Item '{x.id}' does not exist or is not a variation.",
                Args: (IReadOnlyDictionary<string, object>?)null))
            .ToList();
        return errors.Count > 0 ? Problem(AppProblems.UnprocessableEntities(errors)) : null;
    }

    private async Task<IActionResult?> ValidateMemberIds(IReadOnlyList<Guid> memberIds, string field, CancellationToken ct)
    {
        if (memberIds.Count == 0) return null;
        var validIdSet = (await db.CatalogItems
            .Where(c => memberIds.Contains(c.Id) &&
                        (c.Type == CatalogItemType.Standard || c.Type == CatalogItemType.Unit ||
                         c.Type == CatalogItemType.Bundle))
            .Select(c => c.Id)
            .ToListAsync(ct)).ToHashSet();
        var errors = memberIds
            .Select((id, i) => (id, i))
            .Where(x => !validIdSet.Contains(x.id))
            .Select(x => (Field: $"{field}[{x.i}]", Code: ErrorCode.CatalogItemVariationInvalid,
                Message: $"Item '{x.id}' does not exist or is not a standard/unit/bundle item.",
                Args: (IReadOnlyDictionary<string, object>?)null))
            .ToList();
        return errors.Count > 0 ? Problem(AppProblems.UnprocessableEntities(errors)) : null;
    }

    private async Task<IActionResult?> ValidateBundleComponentIds(IReadOnlyList<BundleComponentRequest> components, CancellationToken ct)
    {
        if (components.Count == 0) return null;
        var reqComponentIds = components.Select(c => c.ComponentId).Distinct().ToList();
        var validIds = await db.CatalogItems
            .Where(c => reqComponentIds.Contains(c.Id) &&
                        (c.Type == CatalogItemType.Standard || c.Type == CatalogItemType.Unit ||
                         c.Type == CatalogItemType.ProductGroup || c.Type == CatalogItemType.Variation))
            .Select(c => c.Id)
            .ToListAsync(ct);
        var errors = components
            .Select((c, i) => (c, i))
            .Where(x => !validIds.Contains(x.c.ComponentId))
            .Select(x => (Field: $"components[{x.i}].componentId", Code: ErrorCode.CatalogItemComponentInvalid,
                Message: $"Component '{x.c.ComponentId}' does not exist or cannot be used as a bundle component.",
                Args: (IReadOnlyDictionary<string, object>?)null))
            .ToList();
        return errors.Count > 0 ? Problem(AppProblems.UnprocessableEntities(errors)) : null;
    }

    private async Task<IActionResult?> ValidateExistingBundleComponentIds(IReadOnlyList<BundleComponentRequest> components, Guid bundleId, CancellationToken ct)
    {
        var existingIds = components.Where(c => c.Id.HasValue).Select(c => c.Id!.Value).ToList();
        if (existingIds.Count == 0) return null;
        var validIds = await db.BundleComponents
            .Where(bc => bc.BundleId == bundleId && existingIds.Contains(bc.Id))
            .Select(bc => bc.Id)
            .ToListAsync(ct);
        var errors = components
            .Select((c, i) => (c, i))
            .Where(x => x.c.Id.HasValue && !validIds.Contains(x.c.Id.Value))
            .Select(x => (Field: $"components[{x.i}].id", Code: ErrorCode.CatalogItemComponentNotFound,
                Message: $"Bundle component '{x.c.Id}' not found in this bundle.",
                Args: (IReadOnlyDictionary<string, object>?)null))
            .ToList();
        return errors.Count > 0 ? Problem(AppProblems.UnprocessableEntities(errors)) : null;
    }

    private async Task<IActionResult?> SyncGroupChildren(
        IReadOnlyList<ProductGroupChildRequest> children,
        Guid groupId,
        IReadOnlyList<Guid> groupTagIds,
        bool groupIsArchived,
        CancellationToken ct)
    {
        // Validate types — only Standard/Unit allowed as group children
        var typeErrors = children
            .Select((c, i) => (c, i))
            .Where(x => x.c.Type != CatalogItemType.Standard && x.c.Type != CatalogItemType.Unit)
            .Select(x => (
                Field: $"children[{x.i}].type",
                Code: ErrorCode.CatalogItemGroupInvalid,
                Message: "Only Standard and Unit items can be children of a product group.",
                Args: (IReadOnlyDictionary<string, object>?)null))
            .ToList();
        if (typeErrors.Count > 0) return Problem(AppProblems.UnprocessableEntities(typeErrors));

        var existingChildren = await db.CatalogItems
            .Where(c => c.GroupId == groupId)
            .Include(c => c.Tags)
            .Include(c => c.VariationMemberships)
            .Include(c => c.Images)
            .ToListAsync(ct);
        var existingById = existingChildren.ToDictionary(c => c.Id);

        // Validate IDs in request — must belong to this group
        var idErrors = children
            .Select((c, i) => (c, i))
            .Where(x => x.c.Id.HasValue && !existingById.ContainsKey(x.c.Id.Value))
            .Select(x => (
                Field: $"children[{x.i}].id",
                Code: ErrorCode.CatalogItemNotFound,
                Message: $"Child item '{x.c.Id}' not found in this product group.",
                Args: (IReadOnlyDictionary<string, object>?)null))
            .ToList();
        if (idErrors.Count > 0) return Problem(AppProblems.UnprocessableEntities(idErrors));

        // Validate type immutability for existing children
        var typeChangeErrors = children
            .Select((c, i) => (c, i))
            .Where(x => x.c.Id.HasValue && existingById[x.c.Id.Value].Type != x.c.Type)
            .Select(x => (
                Field: $"children[{x.i}].type",
                Code: ErrorCode.CatalogItemIsImmutable,
                Message: "The type of a catalog item cannot be changed.",
                Args: (IReadOnlyDictionary<string, object>?)null))
            .ToList();
        if (typeChangeErrors.Count > 0) return Problem(AppProblems.UnprocessableEntities(typeChangeErrors));

        // Validate deletions (children in DB not present in request)
        var requestIdSet = children.Where(c => c.Id.HasValue).Select(c => c.Id!.Value).ToHashSet();
        var toDelete = existingChildren.Where(c => !requestIdSet.Contains(c.Id)).ToList();
        if (toDelete.Count > 0)
        {
            var deleteIds = toDelete.Select(c => c.Id).ToList();
            var inUseInGroup = await db.StoragePlacesNodesItemsGroups
                .AnyAsync(g => deleteIds.Contains(g.CatalogItemId) && g.Count > 0, ct);
            var inUseInInventory = await db.InventoryItems
                .AnyAsync(i => deleteIds.Contains(i.CatalogItemId), ct);
            if (inUseInGroup || inUseInInventory)
                return Conflict(ErrorCode.CatalogItemIsInUse,
                    "One or more children cannot be removed because they are stored in a warehouse.");
        }

        // Validate article/barcode uniqueness
        var duplicateErrors = new List<(string, ErrorCode, string, IReadOnlyDictionary<string, object>?)>();

        var articleGroups = children.Select((c, i) => (c.Article, i)).GroupBy(x => x.Article);
        foreach (var g in articleGroups.Where(x => x.Count() > 1))
            foreach (var (_, idx) in g)
                duplicateErrors.Add(($"children[{idx}].article", ErrorCode.CatalogItemArticleDuplicate,
                    $"Duplicate article '{g.Key}' within children list.", null));

        var barcodeGroups = children
            .Select((c, i) => (c.Barcode, i))
            .Where(x => x.Barcode != null)
            .GroupBy(x => x.Barcode!);
        foreach (var g in barcodeGroups.Where(x => x.Count() > 1))
            foreach (var (_, idx) in g)
                duplicateErrors.Add(($"children[{idx}].barcode", ErrorCode.CatalogItemBarcodeDuplicate,
                    $"Duplicate barcode '{g.Key}' within children list.", null));

        // Batch DB check — exclude updated children (old values) and deleted children (being removed in same request)
        var excludeFromDuplicateCheck = requestIdSet.Concat(toDelete.Select(c => c.Id)).ToHashSet();

        var requestArticles = children.Select(c => c.Article).Distinct().ToList();
        var conflictingArticles = (await db.CatalogItems
            .Where(c => requestArticles.Contains(c.Article) && !excludeFromDuplicateCheck.Contains(c.Id))
            .Select(c => c.Article)
            .ToListAsync(ct)).ToHashSet();
        foreach (var (c, i) in children.Select((c, i) => (c, i)))
            if (conflictingArticles.Contains(c.Article))
                duplicateErrors.Add(($"children[{i}].article", ErrorCode.CatalogItemArticleDuplicate,
                    $"A catalog item with article '{c.Article}' already exists.", null));

        var requestBarcodes = children.Select(c => c.Barcode).Where(b => b != null).Select(b => b!).Distinct().ToList();
        if (requestBarcodes.Count > 0)
        {
            var conflictingBarcodes = (await db.CatalogItems
                .Where(c => c.Barcode != null && requestBarcodes.Contains(c.Barcode) && !excludeFromDuplicateCheck.Contains(c.Id))
                .Select(c => c.Barcode!)
                .ToListAsync(ct)).ToHashSet();
            foreach (var (c, i) in children.Select((c, i) => (c, i)))
                if (c.Barcode != null && conflictingBarcodes.Contains(c.Barcode))
                    duplicateErrors.Add(($"children[{i}].barcode", ErrorCode.CatalogItemBarcodeDuplicate,
                        $"A catalog item with barcode '{c.Barcode}' already exists.", null));
        }

        if (duplicateErrors.Count > 0) return Problem(AppProblems.UnprocessableEntities(duplicateErrors));

        // Apply: delete removed children
        foreach (var child in toDelete)
            db.CatalogItems.Remove(child);

        // Apply: update or create children
        foreach (var (req, _) in children.Select((c, i) => (c, i)))
        {
            if (req.Id.HasValue)
            {
                var existing = existingById[req.Id.Value];
                existing.Name = req.Name;
                existing.Article = req.Article;
                existing.Barcode = req.Barcode;
                existing.Description = req.Description;
                existing.Notes = req.Notes;
                existing.LabelText = req.LabelText;
                existing.IsArchived = groupIsArchived;

                var combinedTagIds = req.Tags.Concat(groupTagIds).Distinct().ToList();
                var childTags = await db.CatalogItemTags.Where(t => combinedTagIds.Contains(t.Id)).ToListAsync(ct);
                existing.Tags.Clear();
                foreach (var tag in childTags)
                    existing.Tags.Add(tag);

                var childImageProblem = await BindChildImagesAsync(req, existing, ct);
                if (childImageProblem is not null) return Problem(childImageProblem);
            }
            else
            {
                var combinedTagIds = req.Tags.Concat(groupTagIds).Distinct().ToList();
                var childTags = await db.CatalogItemTags.Where(t => combinedTagIds.Contains(t.Id)).ToListAsync(ct);
                var newChild = new CatalogItem
                {
                    Id = Guid.NewGuid(),
                    Type = req.Type,
                    Name = req.Name,
                    Article = req.Article,
                    Barcode = req.Barcode,
                    Description = req.Description,
                    Notes = req.Notes,
                    LabelText = req.LabelText,
                    IsArchived = groupIsArchived,
                    GroupId = groupId,
                    Tags = childTags
                };

                var childImageProblem = await BindChildImagesAsync(req, newChild, ct);
                if (childImageProblem is not null) return Problem(childImageProblem);

                db.CatalogItems.Add(newChild);
            }
        }

        return null;
    }

    private void SyncVariationMembers(CatalogItem item, IReadOnlyList<Guid> memberIds, Guid variationId)
    {
        var requestIds = memberIds.ToHashSet();
        var toRemove = item.VariationMembers.Where(m => !requestIds.Contains(m.ItemId)).ToList();
        foreach (var m in toRemove)
            db.CatalogItemVariationMembers.Remove(m);
        var existingIds = item.VariationMembers.Select(m => m.ItemId).ToHashSet();
        foreach (var mid in requestIds.Where(m => !existingIds.Contains(m)))
            item.VariationMembers.Add(new CatalogItemVariationMember { ItemId = mid, VariationId = variationId });
    }

    private void SyncBundleComponents(CatalogItem item, IReadOnlyList<BundleComponentRequest> components, Guid bundleId)
    {
        var requestIds = components.Where(c => c.Id.HasValue).Select(c => c.Id!.Value).ToHashSet();
        var toRemove = item.BundleComponents.Where(bc => !requestIds.Contains(bc.Id)).ToList();
        foreach (var bc in toRemove)
            item.BundleComponents.Remove(bc);

        foreach (var req in components)
        {
            if (req.Id.HasValue)
            {
                var existing = item.BundleComponents.First(bc => bc.Id == req.Id.Value);
                existing.ComponentId = req.ComponentId;
                existing.Quantity = req.Quantity;
            }
            else
            {
                db.BundleComponents.Add(new BundleComponent
                {
                    Id = Guid.NewGuid(),
                    BundleId = bundleId,
                    ComponentId = req.ComponentId,
                    Quantity = req.Quantity
                });
            }
        }
    }

    private async Task<List<(string Field, ErrorCode Code, string Message, IReadOnlyDictionary<string, object>? Args)>>
        ValidateDuplicates(string article, string? barcode, Guid? excludeItemId, CancellationToken ct)
    {
        var errors = new List<(string, ErrorCode, string, IReadOnlyDictionary<string, object>?)>();

        var articleExists = await db.CatalogItems
            .AnyAsync(c => c.Article == article && (excludeItemId == null || c.Id != excludeItemId), ct);
        if (articleExists)
            errors.Add(("article", ErrorCode.CatalogItemArticleDuplicate,
                $"A catalog item with article '{article}' already exists.", null));

        if (barcode is not null)
        {
            var barcodeExists = await db.CatalogItems
                .AnyAsync(c => c.Barcode == barcode && (excludeItemId == null || c.Id != excludeItemId), ct);
            if (barcodeExists)
                errors.Add(("barcode", ErrorCode.CatalogItemBarcodeDuplicate,
                    $"A catalog item with barcode '{barcode}' already exists.", null));
        }

        return errors;
    }
}
