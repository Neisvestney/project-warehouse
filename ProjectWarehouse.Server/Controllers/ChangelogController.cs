using System.ComponentModel.DataAnnotations;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProjectWarehouse.Server.Data;
using ProjectWarehouse.Server.Domain;
using ProjectWarehouse.Server.Infrastructure;
using ProjectWarehouse.Server.Models;
using ProjectWarehouse.Server.Models.ChangeLog;

namespace ProjectWarehouse.Server.Controllers;

[Route("api/changelog")]
public class ChangelogController(ApplicationDbContext db, IMapper mapper) : AppControllerBase
{
    /// <summary>List changelog entries (paginated).</summary>
    /// <remarks>
    /// Query params: <c>page</c> (default 1), <c>pageSize</c> (default 20, max 200),
    /// <c>entityType</c> (optional), <c>changeLogEntryType</c> (optional).
    /// Returns <c>Paginated&lt;ChangeLogEntryDto&gt;</c> ordered by <c>createdAt</c> descending.
    /// Requires <c>changelog.view</c> — the log spans every entity type and is not narrowed by assigned
    /// warehouses, so the permission is the only gate.
    /// No error codes beyond 403 <c>permissionDenied</c>; an unparseable <c>entityType</c> or
    /// <c>changeLogEntryType</c> is a model-binding 422 (<c>invalidFormat</c>).
    /// </remarks>
    [HttpGet]
    [Authorize(Policy = Permissions.ChangeLog.View)]
    [ProducesResponseType<Paginated<ChangeLogEntryDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(
        [FromQuery][Range(1, int.MaxValue)] int page = 1,
        [FromQuery][Range(1, 200)] int pageSize = 20,
        [FromQuery] AppEntityType? entityType = null,
        [FromQuery] ChangeLogEntryType? changeLogEntryType = null,
        CancellationToken ct = default)
    {
        var query = db.ChangeLogEntries
            .Include(e => e.User)
            .AsQueryable();

        if (entityType is not null)
            query = query.Where(e => e.EntityType == entityType);

        if (changeLogEntryType is not null)
            query = query.Where(e => e.ChangeLogEntryType == changeLogEntryType);

        var paginated = await query
            .OrderByDescending(e => e.CreatedAt)
            .ProjectTo<ChangeLogEntryDto>(mapper.ConfigurationProvider)
            .ToPaginatedAsync(page, pageSize, ct);

        return Ok(paginated);
    }
}
