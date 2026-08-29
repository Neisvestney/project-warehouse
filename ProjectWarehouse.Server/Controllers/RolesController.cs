using AutoMapper;
using AutoMapper.QueryableExtensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProjectWarehouse.Server.Data;
using ProjectWarehouse.Server.Domain;
using ProjectWarehouse.Server.Infrastructure;
using ProjectWarehouse.Server.Infrastructure.ChangeLog;
using ProjectWarehouse.Server.Infrastructure.Observability;
using ProjectWarehouse.Server.Models;
using ProjectWarehouse.Server.Models.Roles;
using ProjectWarehouse.Server.Services;

namespace ProjectWarehouse.Server.Controllers;

[Route("api/roles")]
public class RolesController(
    RoleManager<ApplicationRole> roleManager,
    ApplicationDbContext db,
    IPermissionService permissionService,
    IMapper mapper,
    IChangeLogService<RolesListDto> changeLog) : AppControllerBase
{
    /// <summary>List all roles with their permissions.</summary>
    /// <remarks>
    /// Requires <c>roles.view</c>. Not paginated — ordered by <c>order</c> then name.
    /// No error codes beyond 403 <c>permissionDenied</c>.
    /// </remarks>
    [HttpGet]
    [Authorize(Policy = Permissions.Roles.View)]
    [ProducesResponseType<IReadOnlyList<RoleWithPermissionsDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken ct = default)
    {
        var roles = await db.Roles
            .OrderBy(r => r.Order).ThenBy(r => r.Name)
            .ProjectTo<RoleWithPermissionsDto>(mapper.ConfigurationProvider)
            .ToListAsync(ct);
        return Ok(roles);
    }

    /// <summary>Search roles by name (id + name only, max 10 results).</summary>
    /// <remarks>
    /// Query params: <c>searchString</c> (optional — omitted returns the first 10 by name).
    /// Requires <c>roles.view</c>. No error codes beyond 403 <c>permissionDenied</c>.
    /// </remarks>
    [HttpGet("search")]
    [Authorize(Policy = Permissions.Roles.View)]
    [ProducesResponseType<IReadOnlyList<RoleDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Search([FromQuery] string? searchString = null, CancellationToken ct = default)
    {
        var roles = await roleManager.Roles
            .WhereMatchesSearch(r => r.Name!, searchString)
            .OrderBy(r => r.Name)
            .Take(10)
            .ProjectTo<RoleDto>(mapper.ConfigurationProvider)
            .ToListAsync(ct);
        return Ok(roles);
    }

    /// <summary>Get a role by ID.</summary>
    /// <remarks>Requires <c>roles.view</c>. Returns 404 <c>roleNotFound</c> if the role does not exist.</remarks>
    [HttpGet("{id:guid}")]
    [Authorize(Policy = Permissions.Roles.View)]
    [ProducesResponseType<RoleDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct = default)
    {
        var dto = await roleManager.Roles
            .Where(r => r.Id == id)
            .ProjectTo<RoleDto>(mapper.ConfigurationProvider)
            .FirstOrDefaultAsync(ct);
        if (dto is null)
            return NotFound(ErrorCode.RoleNotFound, "Role not found.");
        return Ok(dto);
    }

    /// <summary>Atomically replace the entire roles collection.</summary>
    /// <remarks>
    /// Requires <c>roles.edit</c>. Body: <c>UpdateRoleItem[]</c> — the whole collection, not a delta:
    /// <list type="bullet">
    ///   <item><c>id: null</c> or <c>Guid.Empty</c> — create the role</item>
    ///   <item><c>id</c> present — update name, order and permissions (permissions are a full replace)</item>
    ///   <item>an existing role absent from the list — delete it</item>
    /// </list>
    /// Everything is applied in one transaction; affected users' permission caches are bumped afterwards.
    /// The Admin role (normalized name <c>ADMIN</c>) is protected: it cannot be deleted, renamed, or have
    /// permissions taken away.
    /// Error field paths use array-index notation into the request, e.g. <c>[2].permissions[0]</c>.
    /// Error codes:
    /// <list type="bullet">
    ///   <item>422 <c>permissionNotFound</c> (field <c>[i].permissions[j]</c>) — a string not in <c>Permissions.All</c>; every offender is reported</item>
    ///   <item>422 <c>roleNotFound</c> (field <c>id</c>) — an item carries an id that does not exist</item>
    ///   <item>403 <c>roleProtected</c> — Admin deleted, renamed, or stripped of permissions; args <c>{ roleName }</c></item>
    ///   <item>422 <c>validationError</c> (field <c>root</c>) — Identity refused a create, update or delete (e.g. duplicate role name)</item>
    /// </list>
    /// </remarks>
    [HttpPut]
    [Authorize(Policy = Permissions.Roles.Edit)]
    [ProducesResponseType<IReadOnlyList<RoleWithPermissionsDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> UpdateAll([FromBody] List<UpdateRoleItem> request, CancellationToken ct = default)
    {
        var permErrors = new List<(string, ErrorCode, string, IReadOnlyDictionary<string, object>?)>();
        for (var i = 0; i < request.Count; i++)
        for (var j = 0; j < request[i].Permissions.Count; j++)
        {
            if (!Permissions.All.Contains(request[i].Permissions[j]))
                permErrors.Add(($"[{i}].permissions[{j}]", ErrorCode.PermissionNotFound,
                    $"Unknown permission: '{request[i].Permissions[j]}'", null));
        }
        if (permErrors.Count > 0)
            return Problem(AppProblems.UnprocessableEntities(permErrors));

        var existing = await db.Roles
            .Include(r => r.RolePermissions)
            .ToListAsync(ct);
        var existingById = existing.ToDictionary(r => r.Id);

        var beforeDto = new RolesListDto
        {
            Roles = existing.Select(r => mapper.Map<RoleWithPermissionsDto>(r)).ToList()
        };

        var toCreate = request.Where(r => r.Id is null || r.Id == Guid.Empty).ToList();
        var toUpdate = request.Where(r => r.Id is not null && r.Id != Guid.Empty).ToList();

        var unknownIds = toUpdate.Where(r => !existingById.ContainsKey(r.Id!.Value)).ToList();
        if (unknownIds.Count > 0)
        {
            var errors = unknownIds.Select(r =>
                ("id", ErrorCode.RoleNotFound, $"Role '{r.Id}' does not exist.", (IReadOnlyDictionary<string, object>?)null));
            return Problem(AppProblems.UnprocessableEntities(errors));
        }

        var incomingIds = toUpdate.Select(r => r.Id!.Value).ToHashSet();
        var toDelete = existing.Where(r => !incomingIds.Contains(r.Id)).ToList();

        var adminRole = existing.FirstOrDefault(r => r.NormalizedName == "ADMIN");
        if (adminRole is not null)
        {
            if (toDelete.Any(r => r.NormalizedName == "ADMIN"))
                return Forbidden(ErrorCode.RoleProtected, "The Admin role cannot be deleted.", new Dictionary<string, object> { ["roleName"] = adminRole.Name ?? "" });

            var adminIncoming = toUpdate.FirstOrDefault(r => r.Id == adminRole.Id);
            if (adminIncoming is not null)
            {
                if (roleManager.NormalizeKey(adminIncoming.Name) != adminRole.NormalizedName)
                    return Forbidden(ErrorCode.RoleProtected, "The Admin role cannot be renamed.", new Dictionary<string, object> { ["roleName"] = adminRole.Name ?? "" });

                var removedPerms = adminRole.RolePermissions
                    .Select(rp => rp.Permission)
                    .Except(adminIncoming.Permissions)
                    .Any();
                if (removedPerms)
                    return Forbidden(ErrorCode.RoleProtected, "Permissions cannot be removed from the Admin role.", new Dictionary<string, object> { ["roleName"] = adminRole.Name ?? "" });
            }
        }

        var deleteRoleIds = toDelete.Select(r => r.Id).ToList();
        var usersAffectedByDeletion = deleteRoleIds.Count > 0
            ? await db.UserRoles
                .Where(ur => deleteRoleIds.Contains(ur.RoleId))
                .Select(ur => ur.UserId)
                .Distinct()
                .ToListAsync(ct)
            : (List<Guid>)[];

        var bumpRoleIds = new HashSet<Guid>();
        var createdRoles = new List<(ApplicationRole Role, List<string> Permissions)>();

        try
        {
            await db.Database.ExecuteInTransactionAsync("roles.batch_update", async () =>
            {
                foreach (var item in toCreate)
                {
                    var role = new ApplicationRole { Name = item.Name, Order = item.Order };
                    IdentityOperationException.ThrowIfFailed(await roleManager.CreateAsync(role));
                    createdRoles.Add((role, item.Permissions.ToList()));
                    if (item.Permissions.Count > 0)
                        bumpRoleIds.Add(role.Id);
                }

                foreach (var item in toUpdate)
                {
                    var role = existingById[item.Id!.Value];
                    var nameChanged = role.NormalizedName != roleManager.NormalizeKey(item.Name);
                    var orderChanged = role.Order != item.Order;

                    if (nameChanged || orderChanged)
                    {
                        role.Name = item.Name;
                        role.Order = item.Order;
                        IdentityOperationException.ThrowIfFailed(await roleManager.UpdateAsync(role));
                    }
                }

                foreach (var role in toDelete)
                    IdentityOperationException.ThrowIfFailed(await roleManager.DeleteAsync(role));

                foreach (var (role, permissions) in createdRoles)
                    db.RolePermissions.AddRange(permissions.Select(p => new RolePermission { RoleId = role.Id, Permission = p }));

                foreach (var item in toUpdate)
                {
                    var role = existingById[item.Id!.Value];
                    var current = role.RolePermissions.Select(rp => rp.Permission).ToHashSet();
                    var requested = item.Permissions.ToHashSet();

                    var toAdd = requested.Except(current).ToList();
                    var toRemove = current.Except(requested).ToList();

                    if (toAdd.Count > 0)
                    {
                        db.RolePermissions.AddRange(toAdd.Select(p => new RolePermission { RoleId = role.Id, Permission = p }));
                        bumpRoleIds.Add(role.Id);
                    }
                    if (toRemove.Count > 0)
                    {
                        db.RolePermissions.RemoveRange(role.RolePermissions.Where(rp => toRemove.Contains(rp.Permission)));
                        bumpRoleIds.Add(role.Id);
                    }
                }

                await db.SaveChangesAsync(ct);
            }, ct);
        }
        catch (IdentityOperationException ex)
        {
            return Problem(AppProblems.UnprocessableEntities(ex.ToFieldErrors()));
        }

        await permissionService.BumpUsersAsync(usersAffectedByDeletion);

        foreach (var roleId in bumpRoleIds)
            await permissionService.BumpForRoleUsersAsync(roleId);

        var updated = await db.Roles
            .OrderBy(r => r.Order).ThenBy(r => r.Name)
            .ProjectTo<RoleWithPermissionsDto>(mapper.ConfigurationProvider)
            .ToListAsync(ct);

        await changeLog.CompareAndSaveToChangelog(beforeDto, new RolesListDto { Roles = updated });

        return Ok(updated);
    }
}
