using System.ComponentModel.DataAnnotations;
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
            .FirstOrDefaultAsync(u => u.Id == id, ct);

    /// <summary>List all users (paginated).</summary>
    [HttpGet]
    [Authorize(Policy = Permissions.Users.View)]
    [ProducesResponseType<Paginated<UserDetailDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(
        [FromQuery] [Range(1, int.MaxValue)] int page = 1,
        [FromQuery] [Range(1, 200)] int pageSize = 20,
        [FromQuery] string? searchString = null,
        [FromQuery] Guid? role = null,
        CancellationToken ct = default)
    {
        var users = db.Users
            .WhereMatchesSearch(u => u.SearchString, searchString);

        if (role is { } r)
        {
            users = users.Where(x => x.UserRoles.Any(ur => ur.RoleId == r));
        }

        users = users.OrderBy(u => u.Id);

        var paginated = await users
            .ProjectTo<UserDetailDto>(mapper.ConfigurationProvider)
            .ToPaginatedAsync(page, pageSize, ct);

        return Ok(paginated);
    }

    /// <summary>Get a user by ID.</summary>
    [HttpGet("{id:guid}")]
    [Authorize(Policy = Permissions.Users.View)]
    [ProducesResponseType<UserDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct = default)
    {
        var dto = await db.Users
            .Where(u => u.Id == id)
            .ProjectTo<UserDetailDto>(mapper.ConfigurationProvider)
            .FirstOrDefaultAsync(ct);
        if (dto is null)
            return NotFound(ErrorCode.UserNotFound, "User not found.");

        return Ok(dto);
    }

    /// <summary>Create a new user.</summary>
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

        // Authorization checks before any mutations
        if ((rolesChanged || permissionsChanged) &&
            !User.HasClaim("permission", Permissions.Users.ManageRolesAndPermissions))
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

        // All mutations inside a single transaction
        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        user.Email = request.Email;
        user.FirstName = request.FirstName;
        user.LastName = request.LastName;

        var profileResult = await userManager.UpdateAsync(user);
        if (!profileResult.Succeeded)
        {
            var errors = profileResult.Errors.Select(e => ("root", ErrorCode.ValidationError, e.Description,
                (IReadOnlyDictionary<string, object>?)null));
            return Problem(AppProblems.UnprocessableEntities(errors));
        }

        foreach (var role in rolesToAdd)
        {
            var result = await userManager.AddToRoleAsync(user, role.Name!);
            if (!result.Succeeded)
            {
                var errors = result.Errors.Select(e => ("root", ErrorCode.ValidationError, e.Description,
                    (IReadOnlyDictionary<string, object>?)null));
                return Problem(AppProblems.UnprocessableEntities(errors));
            }
        }

        foreach (var role in rolesToRemove)
        {
            var result = await userManager.RemoveFromRoleAsync(user, role.Name!);
            if (!result.Succeeded)
            {
                var errors = result.Errors.Select(e => ("root", ErrorCode.ValidationError, e.Description,
                    (IReadOnlyDictionary<string, object>?)null));
                return Problem(AppProblems.UnprocessableEntities(errors));
            }
        }

        if (toAddPerms.Count > 0)
            db.UserPermissions.AddRange(toAddPerms.Select(p => new UserPermission { UserId = id, Permission = p }));

        if (toRemovePerms.Count > 0)
            db.UserPermissions.RemoveRange(user.UserPermissions.Where(up => toRemovePerms.Contains(up.Permission)));

        if (permissionsChanged)
            await db.SaveChangesAsync(ct);

        await transaction.CommitAsync(ct);

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