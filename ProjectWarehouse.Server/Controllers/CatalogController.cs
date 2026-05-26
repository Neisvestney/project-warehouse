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

namespace ProjectWarehouse.Server.Controllers;

[Route("api/catalog")]
public class CatalogController(
    ApplicationDbContext db,
    IMapper mapper,
    IChangeLogService<CatalogItemDto> changeLog) : AppControllerBase
{
    /// <summary>List all catalog item tags, optionally filtered by name.</summary>
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
    [HttpPost("tags")]
    [Authorize(Policy = Permissions.Catalog.Edit)]
    [ProducesResponseType<CatalogItemTagDto>(StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateTag([FromBody] CreateCatalogItemTagRequest request, CancellationToken ct = default)
    {
        var tag = new CatalogItemTag { Id = Guid.NewGuid(), Name = request.Name.Trim() };
        db.CatalogItemTags.Add(tag);
        await db.SaveChangesAsync(ct);
        var dto = new CatalogItemTagDto { Id = tag.Id, Name = tag.Name };
        return Created($"/api/catalog/tags/{tag.Id}", dto);
    }

    /// <summary>List all catalog items (paginated, optionally filtered by name).</summary>
    [HttpGet]
    [Authorize(Policy = Permissions.Catalog.View)]
    [ProducesResponseType<Paginated<CatalogItemSummaryDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(
        [FromQuery][Range(1, int.MaxValue)] int page = 1,
        [FromQuery][Range(1, 200)] int pageSize = 20,
        [FromQuery] string? searchString = null,
        [FromQuery] CatalogSortBy sortBy = CatalogSortBy.Name,
        [FromQuery] SortOrder sortOrder = SortOrder.Asc,
        CancellationToken ct = default)
    {
        var baseQuery = db.CatalogItems
            .Where(c => c.GroupId == null)
            .WhereMatchesSearch(c => c.SearchString, searchString)
            .OrderBy(c => c.IsArchived);

        var query = sortBy switch
        {
            CatalogSortBy.Article => baseQuery.ThenSort(c => c.Article, sortOrder),
            CatalogSortBy.Barcode => baseQuery.ThenSort(c => c.Barcode, sortOrder),
            CatalogSortBy.Type    => baseQuery.ThenSort(c => c.Type, sortOrder),
            _                     => baseQuery.ThenSort(c => c.Name, sortOrder),
        };

        var paginated = await query
            .ProjectTo<CatalogItemSummaryDto>(mapper.ConfigurationProvider)
            .ToPaginatedAsync(page, pageSize, ct);

        return Ok(paginated);
    }

    /// <summary>Get a catalog item by ID.</summary>
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
    [HttpPost]
    [Authorize(Policy = Permissions.Catalog.Edit)]
    [ProducesResponseType<CatalogItemDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Create([FromBody] CreateCatalogItemRequest request, CancellationToken ct = default)
    {
        if (request.Type == CatalogItemType.AssembledBundle)
            return UnprocessableEntity("type", ErrorCode.CatalogItemIsImmutable,
                "Assembled bundles are created by the assembly process, not through the catalog.");

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

        db.CatalogItems.Add(item);
        await db.SaveChangesAsync(ct);

        var created = await LoadItemWithDetailsAsync(item.Id, ct);
        var dto = mapper.Map<CatalogItemDto>(created!);
        await changeLog.CompareAndSaveToChangelog(null, dto);

        return CreatedAtAction(nameof(GetById), new { id = item.Id }, dto);
    }

    /// <summary>Update a catalog item.</summary>
    /// <remarks>
    /// Assembled bundles are immutable and cannot be updated (returns 422).
    /// Type-specific fields:
    /// <list type="bullet">
    ///   <item><b>Standard / Unit</b>: <c>groupId</c>, <c>variationIds</c> (full replace)</item>
    ///   <item><b>Variation</b>: <c>memberIds</c> (full replace)</item>
    ///   <item><b>Bundle</b>: <c>components</c> — <c>id: null</c> creates, <c>id</c> present updates, missing existing entries are deleted</item>
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
            .Include(c => c.Group)
            .Include(c => c.Tags)
            .Include(c => c.BundleComponents).ThenInclude(bc => bc.Component)
            .Include(c => c.VariationMemberships)
            .Include(c => c.VariationMembers)
            .FirstOrDefaultAsync(c => c.Id == id, ct);

        if (item is null)
            return NotFound(ErrorCode.CatalogItemNotFound, "Catalog item not found.");

        if (item.Type == CatalogItemType.AssembledBundle)
            return UnprocessableEntity("root", ErrorCode.CatalogItemIsImmutable,
                "Assembled bundles cannot be modified.");

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
        item.IsArchived = request.IsArchived;

        var newTags = await db.CatalogItemTags.Where(t => request.Tags.Contains(t.Id)).ToListAsync(ct);
        item.Tags.Clear();
        foreach (var tag in newTags)
            item.Tags.Add(tag);

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

        await db.SaveChangesAsync(ct);

        var afterItem = await LoadItemWithDetailsAsync(id, ct);
        var afterDto = mapper.Map<CatalogItemDto>(afterItem!);
        await changeLog.CompareAndSaveToChangelog(beforeDto, afterDto);

        return Ok(afterDto);
    }

    /// <summary>Delete a catalog item.</summary>
    /// <remarks>Returns 409 <c>catalogItemIsInUse</c> if the item is currently stored in any warehouse.</remarks>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = Permissions.Catalog.Edit)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct = default)
    {
        var item = await db.CatalogItems
            .Include(c => c.Group)
            .FirstOrDefaultAsync(c => c.Id == id, ct);

        if (item is null)
            return NotFound(ErrorCode.CatalogItemNotFound, "Catalog item not found.");

        var isInItemsGroup = await db.StoragePlacesNodesItemsGroups
            .AnyAsync(g => g.CatalogItemId == id && g.Count > 0, ct);
        var isInInventory = await db.InventoryItems
            .AnyAsync(i => i.CatalogItemId == id, ct);

        if (isInItemsGroup || isInInventory)
            return Conflict(ErrorCode.CatalogItemIsInUse, "Cannot delete a catalog item that is stored in a warehouse.");

        var itemForLog = await LoadItemWithDetailsAsync(id, ct);
        var dto = mapper.Map<CatalogItemDto>(itemForLog!);

        db.CatalogItems.Remove(item);
        await db.SaveChangesAsync(ct);

        await changeLog.CompareAndSaveToChangelog(dto, null);

        return NoContent();
    }

    private Task<CatalogItem?> LoadItemWithDetailsAsync(Guid id, CancellationToken ct) =>
        db.CatalogItems
            .Include(c => c.Group)
            .Include(c => c.Tags)
            .Include(c => c.BundleComponents).ThenInclude(bc => bc.Component)
            .Include(c => c.VariationMemberships)
            .Include(c => c.VariationMembers)
            .Include(c => c.GroupChildren).ThenInclude(child => child.Tags)
            .Include(c => c.GroupChildren).ThenInclude(child => child.VariationMemberships)
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
                        (c.Type == CatalogItemType.Standard || c.Type == CatalogItemType.Unit))
            .Select(c => c.Id)
            .ToListAsync(ct)).ToHashSet();
        var errors = memberIds
            .Select((id, i) => (id, i))
            .Where(x => !validIdSet.Contains(x.id))
            .Select(x => (Field: $"{field}[{x.i}]", Code: ErrorCode.CatalogItemVariationInvalid,
                Message: $"Item '{x.id}' does not exist or is not a standard/unit item.",
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
                existing.IsArchived = groupIsArchived;

                var combinedTagIds = req.Tags.Concat(groupTagIds).Distinct().ToList();
                var childTags = await db.CatalogItemTags.Where(t => combinedTagIds.Contains(t.Id)).ToListAsync(ct);
                existing.Tags.Clear();
                foreach (var tag in childTags)
                    existing.Tags.Add(tag);
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
                    IsArchived = groupIsArchived,
                    GroupId = groupId,
                    Tags = childTags
                };
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
