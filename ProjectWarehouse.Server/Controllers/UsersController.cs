using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProjectWarehouse.Server.Data;
using ProjectWarehouse.Server.Domain;
using ProjectWarehouse.Server.Infrastructure;
using ProjectWarehouse.Server.Infrastructure.ChangeLog;
using ProjectWarehouse.Server.Infrastructure.Observability;
using ProjectWarehouse.Server.Models;
using ProjectWarehouse.Server.Models.Users;
using ProjectWarehouse.Server.Services;

namespace ProjectWarehouse.Server.Controllers;

[Route("api/users")]
public class UsersController(
    UserManager<ApplicationUser> userManager,
    ApplicationDbContext db,
    SecurityVersionStore versionStore,
    IMapper mapper,
    IChangeLogService<UserDetailDto> changeLogService) : AppControllerBase
{
    private Task<ApplicationUser?> LoadUserWithDetailsAsync(Guid id, CancellationToken ct = default) =>
        db.Users
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .Include(u => u.UserPermissions)
            .Include(u => u.AssignedWarehouses)
            .FirstOrDefaultAsync(u => u.Id == id, ct);

    /// <summary>List all users (paginated).</summary>
    /// <remarks>
    /// Query params: <c>page</c> (default 1), <c>pageSize</c> (default 20, max 200), <c>searchString</c>,
    /// <c>role</c> (role id), <c>warehouse</c> (assigned warehouse id). Ordered by id.
    /// Requires <c>users.view</c>; without it the request is refused with 403 <c>permissionDenied</c>.
    /// </remarks>
    [HttpGet]
    [Authorize(Policy = Permissions.Users.View)]
    [ProducesResponseType<Paginated<UserDetailDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(
        [FromQuery] [Range(1, int.MaxValue)] int page = 1,
        [FromQuery] [Range(1, 200)] int pageSize = 20,
        [FromQuery] string? searchString = null,
        [FromQuery] Guid? role = null,
        [FromQuery] Guid? warehouse = null,
        CancellationToken ct = default)
    {
        var users = db.Users
            .WhereMatchesSearch(u => u.SearchString, searchString);

        if (role is { } r)
        {
            users = users.Where(x => x.UserRoles.Any(ur => ur.RoleId == r));
        }
        
        if (warehouse is { } w)
        {
            users = users.Where(x => x.AssignedWarehouses.Any(ur => ur.Id == w));
        }

        users = users.OrderBy(u => u.Id);

        var paginated = await users
            .ProjectTo<UserDetailDto>(mapper.ConfigurationProvider)
            .ToPaginatedAsync(page, pageSize, ct);

        return Ok(paginated);
    }

    /// <summary>Get a user by ID.</summary>
    /// <remarks>
    /// Requires <c>users.view</c>, <b>except</b> when <paramref name="id"/> is the caller's own id — everyone
    /// may read their own record, which is what makes the profile page work without the permission.
    /// Returns 403 <c>permissionDenied</c> for someone else's id without <c>users.view</c>, and 404
    /// <c>userNotFound</c> if the user does not exist.
    /// </remarks>
    [HttpGet("{id:guid}")]
    [Authorize]
    [ProducesResponseType<UserDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct = default)
    {
        var rawId = User.FindFirstValue(JwtRegisteredClaimNames.Sub);
        var isSelf = Guid.TryParse(rawId, out var currentUserId) && currentUserId == id;

        if (!isSelf && !User.HasClaim("permission", Permissions.Users.View))
            return Forbidden();

        var dto = await db.Users
            .Where(u => u.Id == id)
            .ProjectTo<UserDetailDto>(mapper.ConfigurationProvider)
            .FirstOrDefaultAsync(ct);
        if (dto is null)
            return NotFound(ErrorCode.UserNotFound, "User not found.");

        return Ok(dto);
    }

    /// <summary>Create a new user.</summary>
    /// <remarks>
    /// Requires <c>users.create</c>. Body: <c>CreateUserRequest</c> — username, password, email, firstName,
    /// lastName. Roles, direct permissions and warehouse assignments are not set here; use
    /// <c>PUT /api/users/{id}</c> afterwards.
    /// Error codes:
    /// <list type="bullet">
    ///   <item>409 <c>userAlreadyExists</c> (field <c>username</c>) — the username is taken</item>
    ///   <item>422 <c>passwordTooShort</c> (field <c>root</c>) — args <c>{ minimalLength }</c></item>
    ///   <item>422 <c>passwordAtLeastOneDigit</c> / <c>passwordAtLeastOneUppercase</c> /
    ///         <c>passwordAtLeastOneLowercase</c> (field <c>root</c>)</item>
    ///   <item>422 <c>validationError</c> (field <c>root</c>) — any other Identity failure</item>
    /// </list>
    /// </remarks>
    [HttpPost]
    [Authorize(Policy = Permissions.Users.Create)]
    [ProducesResponseType<UserDetailDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create([FromBody] CreateUserRequest request)
    {
        var existing = await userManager.FindByNameAsync(request.Username);
        if (existing is not null)
            return ConflictField("username", ErrorCode.UserAlreadyExists, "Username is already taken.");

        var user = new ApplicationUser
        {
            UserName = request.Username,
            Email = request.Email,
            FirstName = request.FirstName,
            LastName = request.LastName,
        };

        var result = await userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            return Problem(PasswordValidationErrorsMapper.MapPasswordValidationErrors(result.Errors));
        }

        var dto = await db.Users
            .Where(u => u.Id == user.Id)
            .ProjectTo<UserDetailDto>(mapper.ConfigurationProvider)
            .FirstAsync();

        await changeLogService.CompareAndSaveToChangelog(null, dto);

        return CreatedAtAction(nameof(GetById), new { id = user.Id }, dto);
    }

    /// <summary>Update a user's profile, roles, and direct permissions atomically.</summary>
    /// <remarks>
    /// Body: <c>UpdateUserRequest</c>. <c>roleIds</c>, <c>directPermissions</c> and <c>assignedWarehouseIds</c>
    /// are full replaces — anything omitted is removed. Profile, roles, permissions and assignments are applied
    /// in one transaction, so a rejected part leaves nothing written.
    /// Permissions: <c>users.edit_profile</c> to call at all; changing <c>roleIds</c> or
    /// <c>directPermissions</c> additionally needs <c>users.manage_roles_and_permissions</c>, and changing
    /// <c>assignedWarehouseIds</c> needs <c>users.manage_assigned_warehouses</c>. The extra permission is only
    /// demanded when the corresponding set actually differs from the stored one, so a plain profile save with
    /// the current roles echoed back is allowed.
    /// A role or permission change bumps the user's <c>security_version</c>, forcing their clients to refresh.
    /// Error codes:
    /// <list type="bullet">
    ///   <item>403 <c>permissionDenied</c> — roles/permissions or warehouses changed without the extra permission</item>
    ///   <item>404 <c>userNotFound</c> — no such user</item>
    ///   <item>422 <c>permissionNotFound</c> (field <c>directPermissions</c>) — a string not in <c>Permissions.All</c>, one error per unknown value</item>
    ///   <item>422 <c>roleNotFound</c> (field <c>roleIds</c>) — one or more role ids do not exist</item>
    ///   <item>422 <c>warehouseNotFound</c> (field <c>assignedWarehouseIds</c>) — one or more warehouse ids do not exist</item>
    ///   <item>422 <c>validationError</c> (field <c>root</c>) — an Identity failure while saving the profile or role membership</item>
    /// </list>
    /// </remarks>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = Permissions.Users.EditProfile)]
    [ProducesResponseType<UserDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateUserRequest request,
        CancellationToken ct = default)
    {
        var user = await LoadUserWithDetailsAsync(id, ct);
        if (user is null)
            return NotFound(ErrorCode.UserNotFound, "User not found.");

        var beforeDto = mapper.Map<UserDetailDto>(user);

        var currentRoleIds = user.UserRoles.Select(ur => ur.RoleId).ToHashSet();
        var requestedRoleIds = request.RoleIds.ToHashSet();
        var rolesChanged = !currentRoleIds.SetEquals(requestedRoleIds);

        var currentPermissions = user.UserPermissions.Select(up => up.Permission).ToHashSet();
        var requestedPermissions = request.DirectPermissions.ToHashSet();
        var permissionsChanged = !currentPermissions.SetEquals(requestedPermissions);

        var currentWarehouseIds = user.AssignedWarehouses.Select(w => w.Id).ToHashSet();
        var requestedWarehouseIds = request.AssignedWarehouseIds.ToHashSet();
        var warehousesChanged = !currentWarehouseIds.SetEquals(requestedWarehouseIds);

        // Authorization checks before any mutations
        if ((rolesChanged || permissionsChanged) &&
            !User.HasClaim("permission", Permissions.Users.ManageRolesAndPermissions))
            return Forbidden();

        if (warehousesChanged && !User.HasClaim("permission", Permissions.Users.ManageAssignedWarehouses))
            return Forbidden();

        // Validate unknown permission strings
        var unknownPermissions = requestedPermissions.Except(Permissions.All).ToList();
        if (unknownPermissions.Count > 0)
        {
            var errors = unknownPermissions.Select(p =>
                ("directPermissions", ErrorCode.PermissionNotFound, $"Unknown permission: '{p}'",
                    (IReadOnlyDictionary<string, object>?)null));
            return Problem(AppProblems.UnprocessableEntities(errors));
        }

        // Pre-load roles to add and validate all IDs exist (before any DB writes)
        var toAddIds = requestedRoleIds.Except(currentRoleIds).ToList();
        var rolesToAdd = toAddIds.Count > 0
            ? await db.Roles.Where(r => toAddIds.Contains(r.Id)).ToListAsync(ct)
            : (List<ApplicationRole>)[];
        if (rolesToAdd.Count != toAddIds.Count)
            return UnprocessableEntity("roleIds", ErrorCode.RoleNotFound, "One or more role IDs do not exist.");

        // Roles to remove are already in memory via the Include — no extra DB query needed
        var toRemoveIds = currentRoleIds.Except(requestedRoleIds).ToHashSet();
        var rolesToRemove = user.UserRoles
            .Where(ur => toRemoveIds.Contains(ur.RoleId))
            .Select(ur => ur.Role)
            .ToList();

        var toAddPerms = requestedPermissions.Except(currentPermissions).ToList();
        var toRemovePerms = currentPermissions.Except(requestedPermissions).ToList();

        var toAddWarehouseIds = requestedWarehouseIds.Except(currentWarehouseIds).ToList();
        var warehousesToAdd = toAddWarehouseIds.Count > 0
            ? await db.Warehouses.Where(w => toAddWarehouseIds.Contains(w.Id)).ToListAsync(ct)
            : [];
        if (warehousesToAdd.Count != toAddWarehouseIds.Count)
            return UnprocessableEntity("assignedWarehouseIds", ErrorCode.WarehouseNotFound,
                "One or more warehouse IDs do not exist.");

        var toRemoveWarehouseIds = currentWarehouseIds.Except(requestedWarehouseIds).ToHashSet();
        var warehousesToRemove = user.AssignedWarehouses.Where(w => toRemoveWarehouseIds.Contains(w.Id)).ToList();

        // All mutations inside a single transaction
        try
        {
            await db.Database.ExecuteInTransactionAsync("users.update", async () =>
            {
                user.Email = request.Email;
                user.FirstName = request.FirstName;
                user.LastName = request.LastName;

                IdentityOperationException.ThrowIfFailed(await userManager.UpdateAsync(user));

                foreach (var role in rolesToAdd)
                    IdentityOperationException.ThrowIfFailed(await userManager.AddToRoleAsync(user, role.Name!));

                foreach (var role in rolesToRemove)
                    IdentityOperationException.ThrowIfFailed(await userManager.RemoveFromRoleAsync(user, role.Name!));

                if (toAddPerms.Count > 0)
                    db.UserPermissions.AddRange(toAddPerms.Select(p => new UserPermission { UserId = id, Permission = p }));

                if (toRemovePerms.Count > 0)
                    db.UserPermissions.RemoveRange(user.UserPermissions.Where(up => toRemovePerms.Contains(up.Permission)));

                foreach (var w in warehousesToAdd) user.AssignedWarehouses.Add(w);
                foreach (var w in warehousesToRemove) user.AssignedWarehouses.Remove(w);

                if (permissionsChanged || warehousesChanged)
                    await db.SaveChangesAsync(ct);
            }, ct);
        }
        catch (IdentityOperationException ex)
        {
            return Problem(AppProblems.UnprocessableEntities(ex.ToFieldErrors()));
        }

        if (rolesChanged || permissionsChanged)
            await versionStore.BumpAsync(user.Id);

        var dto = await db.Users
            .Where(u => u.Id == id)
            .ProjectTo<UserDetailDto>(mapper.ConfigurationProvider)
            .FirstAsync(ct);

        await changeLogService.CompareAndSaveToChangelog(beforeDto, dto);
        
        return Ok(dto);
    }

    /// <summary>Delete a user.</summary>
    /// <remarks>
    /// Requires <c>users.delete</c>. Evicts the user's cached security version, so their outstanding tokens
    /// stop validating.
    /// Returns 404 <c>userNotFound</c> if no such user, and 422 <c>validationError</c> (field <c>root</c>) if
    /// Identity refuses the delete.
    /// </remarks>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = Permissions.Users.Delete)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id)
    {
        var user = await userManager.FindByIdAsync(id.ToString());
        if (user is null)
            return NotFound(ErrorCode.UserNotFound, "User not found.");

        var beforeDto = await db.Users
            .Where(u => u.Id == id)
            .ProjectTo<UserDetailDto>(mapper.ConfigurationProvider)
            .FirstAsync();

        var result = await userManager.DeleteAsync(user);
        if (!result.Succeeded)
        {
            var errors = result.Errors.Select(e => ("root", ErrorCode.ValidationError, e.Description,
                (IReadOnlyDictionary<string, object>?)null));
            return Problem(AppProblems.UnprocessableEntities(errors));
        }

        versionStore.Evict(id);
        await changeLogService.CompareAndSaveToChangelog(beforeDto, null);

        return NoContent();
    }

    /// <summary>Reset another user's password (admin action, no current password required).</summary>
    /// <remarks>
    /// Requires <c>users.reset_password</c>. Bumps the target user's <c>security_version</c>, invalidating
    /// their existing access tokens.
    /// Error codes:
    /// <list type="bullet">
    ///   <item>404 <c>userNotFound</c> — no such user</item>
    ///   <item>422 <c>passwordTooShort</c> (field <c>root</c>) — args <c>{ minimalLength }</c></item>
    ///   <item>422 <c>passwordAtLeastOneDigit</c> / <c>passwordAtLeastOneUppercase</c> /
    ///         <c>passwordAtLeastOneLowercase</c> (field <c>root</c>)</item>
    ///   <item>422 <c>validationError</c> (field <c>root</c>) — any other Identity failure</item>
    /// </list>
    /// <c>passwordInvalid</c> cannot occur here — no current password is checked.
    /// </remarks>
    [HttpPut("{id:guid}/password")]
    [Authorize(Policy = Permissions.Users.ResetPassword)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ChangePassword(Guid id, [FromBody] ChangePasswordRequest request)
    {
        var user = await userManager.FindByIdAsync(id.ToString());
        if (user is null)
            return NotFound(ErrorCode.UserNotFound, "User not found.");

        var token = await userManager.GeneratePasswordResetTokenAsync(user);
        var result = await userManager.ResetPasswordAsync(user, token, request.NewPassword);
        if (!result.Succeeded)
        {
            return Problem(PasswordValidationErrorsMapper.MapPasswordValidationErrors(result.Errors));
        }

        await versionStore.BumpAsync(user.Id);
        return NoContent();
    }
}