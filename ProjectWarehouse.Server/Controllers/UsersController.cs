using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProjectWarehouse.Server.Data;
using ProjectWarehouse.Server.Domain;
using ProjectWarehouse.Server.Infrastructure;
using ProjectWarehouse.Server.Models;
using ProjectWarehouse.Server.Models.Users;
using ProjectWarehouse.Server.Services;

namespace ProjectWarehouse.Server.Controllers;

[Route("api/users")]
public class UsersController(
    UserManager<ApplicationUser> userManager,
    RoleManager<ApplicationRole> roleManager,
    ApplicationDbContext db,
    IPermissionService permissionService,
    SecurityVersionStore versionStore) : AppControllerBase
{
    /// <summary>List all users.</summary>
    [HttpGet]
    [Authorize(Policy = Permissions.Users.View)]
    [ProducesResponseType<IReadOnlyList<UserDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll()
    {
        var users = await userManager.Users
            .Select(u => new UserDto
            {
                Id = u.Id,
                Username = u.UserName!,
                Email = u.Email,
                FirstName = u.FirstName,
                LastName = u.LastName,
            })
            .ToListAsync();
        return Ok(users);
    }

    /// <summary>Get a user by ID.</summary>
    [HttpGet("{id:guid}")]
    [Authorize(Policy = Permissions.Users.View)]
    [ProducesResponseType<UserDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id)
    {
        var user = await userManager.FindByIdAsync(id.ToString());
        if (user is null)
            return NotFound(ErrorCode.UserNotFound, "User not found.");

        return Ok(new UserDto
        {
            Id = user.Id,
            Username = user.UserName!,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
        });
    }

    /// <summary>Create a new user.</summary>
    [HttpPost]
    [Authorize(Policy = Permissions.Users.Create)]
    [ProducesResponseType<UserDto>(StatusCodes.Status201Created)]
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
            var errors = result.Errors.Select(e => ("root", ErrorCode.ValidationError, e.Description, (IReadOnlyDictionary<string, object>?)null));
            return Problem(AppProblems.UnprocessableEntities(errors));
        }

        var dto = new UserDto
        {
            Id = user.Id,
            Username = user.UserName!,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
        };
        return CreatedAtAction(nameof(GetById), new { id = user.Id }, dto);
    }

    /// <summary>Update a user's profile.</summary>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = Permissions.Users.Edit)]
    [ProducesResponseType<UserDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateUserRequest request)
    {
        var user = await userManager.FindByIdAsync(id.ToString());
        if (user is null)
            return NotFound(ErrorCode.UserNotFound, "User not found.");

        user.Email = request.Email;
        user.FirstName = request.FirstName;
        user.LastName = request.LastName;

        var result = await userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            var errors = result.Errors.Select(e => ("root", ErrorCode.ValidationError, e.Description, (IReadOnlyDictionary<string, object>?)null));
            return Problem(AppProblems.UnprocessableEntities(errors));
        }
        return Ok(new UserDto
        {
            Id = user.Id,
            Username = user.UserName!,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
        });
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

        var result = await userManager.DeleteAsync(user);
        if (!result.Succeeded)
        {
            var errors = result.Errors.Select(e => ("root", ErrorCode.ValidationError, e.Description, (IReadOnlyDictionary<string, object>?)null));
            return Problem(AppProblems.UnprocessableEntities(errors));
        }
        versionStore.Evict(user.Id);
        return NoContent();
    }

    /// <summary>Get a user's effective permissions (role + direct).</summary>
    [HttpGet("{id:guid}/permissions")]
    [Authorize(Policy = Permissions.Users.View)]
    [ProducesResponseType<IReadOnlyList<string>>(StatusCodes.Status200OK)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPermissions(Guid id)
    {
        var user = await userManager.FindByIdAsync(id.ToString());
        if (user is null)
            return NotFound(ErrorCode.UserNotFound, "User not found.");

        var permissions = await permissionService.GetEffectivePermissionsAsync(id);
        return Ok(permissions);
    }

    /// <summary>Assign a direct permission to a user.</summary>
    [HttpPost("{id:guid}/permissions")]
    [Authorize(Policy = Permissions.Users.ManagePermissions)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AddPermission(Guid id, [FromBody] AssignPermissionRequest request)
    {
        var user = await userManager.FindByIdAsync(id.ToString());
        if (user is null)
            return NotFound(ErrorCode.UserNotFound, "User not found.");

        if (!Permissions.All.Contains(request.Permission))
            return NotFound(ErrorCode.PermissionNotFound, $"Permission '{request.Permission}' does not exist.");

        var exists = await db.UserPermissions
            .AnyAsync(up => up.UserId == id && up.Permission == request.Permission);
        if (exists)
            return Conflict(ErrorCode.PermissionAlreadyAssigned, "User already has this permission.");

        await permissionService.AddUserPermissionAsync(id, request.Permission);
        return NoContent();
    }

    /// <summary>Remove a direct permission from a user.</summary>
    [HttpDelete("{id:guid}/permissions/{permission}")]
    [Authorize(Policy = Permissions.Users.ManagePermissions)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemovePermission(Guid id, string permission)
    {
        var user = await userManager.FindByIdAsync(id.ToString());
        if (user is null)
            return NotFound(ErrorCode.UserNotFound, "User not found.");

        var exists = await db.UserPermissions
            .AnyAsync(up => up.UserId == id && up.Permission == permission);
        if (!exists)
            return NotFound(ErrorCode.PermissionNotFound, "User does not have this permission.");

        await permissionService.RemoveUserPermissionAsync(id, permission);
        return NoContent();
    }

    /// <summary>Get the roles assigned to a user.</summary>
    [HttpGet("{id:guid}/roles")]
    [Authorize(Policy = Permissions.Users.View)]
    [ProducesResponseType<IReadOnlyList<string>>(StatusCodes.Status200OK)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetRoles(Guid id)
    {
        var user = await userManager.FindByIdAsync(id.ToString());
        if (user is null)
            return NotFound(ErrorCode.UserNotFound, "User not found.");

        var roles = await userManager.GetRolesAsync(user);
        return Ok(roles);
    }

    /// <summary>Assign a role to a user.</summary>
    [HttpPost("{id:guid}/roles")]
    [Authorize(Policy = Permissions.Users.ManageRoles)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AddRole(Guid id, [FromBody] AssignRoleRequest request)
    {
        var user = await userManager.FindByIdAsync(id.ToString());
        if (user is null)
            return NotFound(ErrorCode.UserNotFound, "User not found.");

        var role = await roleManager.FindByIdAsync(request.RoleId.ToString());
        if (role is null)
            return NotFound(ErrorCode.RoleNotFound, "Role not found.");

        var result = await userManager.AddToRoleAsync(user, role.Name!);
        if (!result.Succeeded)
        {
            var errors = result.Errors.Select(e => ("root", ErrorCode.ValidationError, e.Description, (IReadOnlyDictionary<string, object>?)null));
            return Problem(AppProblems.UnprocessableEntities(errors));
        }

        await versionStore.BumpAsync(user.Id);
        return NoContent();
    }

    /// <summary>Remove a role from a user.</summary>
    [HttpDelete("{id:guid}/roles/{roleId:guid}")]
    [Authorize(Policy = Permissions.Users.ManageRoles)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveRole(Guid id, Guid roleId)
    {
        var user = await userManager.FindByIdAsync(id.ToString());
        if (user is null)
            return NotFound(ErrorCode.UserNotFound, "User not found.");

        var role = await roleManager.FindByIdAsync(roleId.ToString());
        if (role is null)
            return NotFound(ErrorCode.RoleNotFound, "Role not found.");

        var result = await userManager.RemoveFromRoleAsync(user, role.Name!);
        if (!result.Succeeded)
        {
            var errors = result.Errors.Select(e => ("root", ErrorCode.ValidationError, e.Description, (IReadOnlyDictionary<string, object>?)null));
            return Problem(AppProblems.UnprocessableEntities(errors));
        }
        await versionStore.BumpAsync(user.Id);
        return NoContent();
    }
}
