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
using ProjectWarehouse.Server.Infrastructure.Concurrency;
using ProjectWarehouse.Server.Models;
using ProjectWarehouse.Server.Models.Organizations;
using ProjectWarehouse.Server.Services;

namespace ProjectWarehouse.Server.Controllers;

[Route("api/organizations")]
public class OrganizationsController(
    ApplicationDbContext db,
    IMapper mapper,
    IOrganizationService organizations,
    IChangeLogService<OrganizationDto> changeLog) : AppControllerBase
{
    /// <summary>List organizations (paginated, searchable).</summary>
    /// <remarks>
    /// Query params: <c>page</c> (default 1), <c>pageSize</c> (default 20, max 200), <c>searchString</c> (name,
    /// legal name, INN), <c>sortBy</c> (default <c>Name</c>), <c>sortOrder</c> (default <c>Asc</c>).
    /// Requires <c>organizations.view</c>; 403 <c>permissionDenied</c> otherwise.
    /// </remarks>
    [HttpGet]
    [Authorize(Policy = Permissions.Organizations.View)]
    [ProducesResponseType<Paginated<OrganizationSummaryDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(
        [FromQuery] [Range(1, int.MaxValue)] int page = 1,
        [FromQuery] [Range(1, 200)] int pageSize = 20,
        [FromQuery] string? searchString = null,
        [FromQuery] OrganizationSortBy sortBy = OrganizationSortBy.Name,
        [FromQuery] SortOrder sortOrder = SortOrder.Asc,
        CancellationToken ct = default)
    {
        var query = db.Organizations.WhereMatchesSearch(o => o.SearchString, searchString);

        var sorted = sortBy switch
        {
            OrganizationSortBy.Inn => query.Sort(o => o.Inn, sortOrder),
            OrganizationSortBy.CreatedAt => query.Sort(o => o.CreatedAt, sortOrder),
            _ => query.Sort(o => o.Name, sortOrder),
        };

        var paginated = await sorted
            .ThenBy(o => o.Id)
            .ProjectTo<OrganizationSummaryDto>(mapper.ConfigurationProvider)
            .ToPaginatedAsync(page, pageSize, ct);

        return Ok(paginated);
    }

    /// <summary>Id, name and INN of every organization, for pickers.</summary>
    /// <remarks>
    /// Query params: <c>searchString</c> (optional). Requires <c>organizations.view</c>; 403
    /// <c>permissionDenied</c> otherwise.
    /// </remarks>
    [HttpGet("short")]
    [Authorize(Policy = Permissions.Organizations.View)]
    [ProducesResponseType<List<OrganizationShortSummaryDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetShort([FromQuery] string? searchString = null, CancellationToken ct = default)
    {
        var list = await db.Organizations
            .WhereMatchesSearch(o => o.SearchString, searchString)
            .OrderBy(o => o.Name)
            .ThenBy(o => o.Id)
            .ProjectTo<OrganizationShortSummaryDto>(mapper.ConfigurationProvider)
            .ToListAsync(ct);

        return Ok(list);
    }

    /// <summary>Organization with its linked marketplace accounts.</summary>
    /// <remarks>Returns 404 <c>organizationNotFound</c>. Requires <c>organizations.view</c>.</remarks>
    [HttpGet("{id:guid}")]
    [Authorize(Policy = Permissions.Organizations.View)]
    [ProducesResponseType<OrganizationDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var dto = await organizations.GetDtoAsync(id, ct);
        return dto is null ? NotFound(ErrorCode.OrganizationNotFound, "Organization not found.") : Ok(dto);
    }

    /// <summary>Creates an organization by hand. Sync links accounts with the same INN to it from then on.</summary>
    /// <remarks>
    /// Body: <c>SaveOrganizationRequest</c>. Errors:
    /// <list type="bullet">
    ///   <item>422 <c>organizationInnInvalid</c> on <c>inn</c> — not 10 or 12 digits</item>
    ///   <item>422 <c>organizationInnDuplicate</c> on <c>inn</c> — another organization has this INN</item>
    /// </list>
    /// Requires <c>organizations.edit</c>.
    /// </remarks>
    [HttpPost]
    [Authorize(Policy = Permissions.Organizations.Edit)]
    [ProducesResponseType<OrganizationDto>(StatusCodes.Status201Created)]
    public async Task<IActionResult> Create([FromBody] SaveOrganizationRequest request, CancellationToken ct)
    {
        if (ValidateInn(request.Inn) is { } innProblem)
            return innProblem;

        var organization = new Organization
        {
            Id = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            CreatedById = GetCurrentUserId(),
        };
        Apply(organization, request);
        db.Organizations.Add(organization);

        if (await SaveAsync(ct) is { } duplicate)
            return duplicate;

        var dto = (await organizations.GetDtoAsync(organization.Id, ct))!;
        await changeLog.CompareAndSaveToChangelog(null, dto);

        return CreatedAtAction(nameof(GetById), new { id = organization.Id }, dto);
    }

    /// <summary>Updates the organization's own requisites. Linked accounts are not touched.</summary>
    /// <remarks>
    /// Body: <c>SaveOrganizationRequest</c>. Errors:
    /// <list type="bullet">
    ///   <item>404 <c>organizationNotFound</c></item>
    ///   <item>422 <c>organizationInnInvalid</c> on <c>inn</c> — not 10 or 12 digits</item>
    ///   <item>422 <c>organizationInnDuplicate</c> on <c>inn</c> — another organization has this INN</item>
    ///   <item>409 <c>entityLocked</c> — another request is changing the organization; nothing was written</item>
    /// </list>
    /// Requires <c>organizations.edit</c>.
    /// </remarks>
    [LocksEntity<Organization>]
    [HttpPut("{id:guid}")]
    [Authorize(Policy = Permissions.Organizations.Edit)]
    [ProducesResponseType<OrganizationDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(Guid id, [FromBody] SaveOrganizationRequest request, CancellationToken ct)
    {
        var organization = await db.Organizations.FirstOrDefaultAsync(o => o.Id == id, ct);
        if (organization is null)
            return NotFound(ErrorCode.OrganizationNotFound, "Organization not found.");

        if (ValidateInn(request.Inn) is { } innProblem)
            return innProblem;

        var before = await organizations.GetDtoAsync(id, ct);

        Apply(organization, request);
        if (await SaveAsync(ct) is { } duplicate)
            return duplicate;

        var after = (await organizations.GetDtoAsync(id, ct))!;
        await changeLog.CompareAndSaveToChangelog(before, after);

        return Ok(after);
    }

    /// <summary>Deletes an organization that no account is linked to.</summary>
    /// <remarks>
    /// Returns 404 <c>organizationNotFound</c>, or 409 <c>organizationHasAccounts</c> while any marketplace
    /// account is linked to it, or 409 <c>entityLocked</c> while another request is changing it.
    /// Requires <c>organizations.edit</c>.
    /// </remarks>
    [LocksEntity<Organization>(ForDelete = true)]
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = Permissions.Organizations.Edit)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var organization = await db.Organizations.FirstOrDefaultAsync(o => o.Id == id, ct);
        if (organization is null)
            return NotFound(ErrorCode.OrganizationNotFound, "Organization not found.");

        // Without this pre-check the Restrict FK raises a raw 23503 and the client gets an unrenderable 500
        if (await db.MarketplaceAccounts.AnyAsync(a => a.OrganizationId == id, ct))
            return Conflict(ErrorCode.OrganizationHasAccounts,
                "Marketplace accounts are linked to the organization.");

        var before = await organizations.GetDtoAsync(id, ct);

        db.Organizations.Remove(organization);
        await db.SaveChangesAsync(ct);

        await changeLog.CompareAndSaveToChangelog(before, null);

        return NoContent();
    }

    private static void Apply(Organization organization, SaveOrganizationRequest request)
    {
        organization.Name = request.Name.Trim();
        organization.Inn = request.Inn.Trim();
        organization.LegalName = Blank(request.LegalName);
        organization.Kpp = Blank(request.Kpp);
        organization.Ogrn = Blank(request.Ogrn);
        organization.OwnershipForm = Blank(request.OwnershipForm);
    }

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private IActionResult? ValidateInn(string inn)
    {
        var trimmed = inn.Trim();
        return trimmed.Length is 10 or 12 && trimmed.All(char.IsAsciiDigit)
            ? null
            : UnprocessableEntity("inn", ErrorCode.OrganizationInnInvalid, "INN must be 10 or 12 digits.");
    }

    private async Task<IActionResult?> SaveAsync(CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
            return null;
        }
        catch (Exception e) when (UniqueViolations.IsOrganizationInn(e))
        {
            return UnprocessableEntity("inn", ErrorCode.OrganizationInnDuplicate,
                "An organization with this INN already exists.");
        }
    }
}
