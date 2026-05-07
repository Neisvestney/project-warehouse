using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProjectWarehouse.Server.Data;
using ProjectWarehouse.Server.Domain;
using ProjectWarehouse.Server.Infrastructure;
using ProjectWarehouse.Server.Models;
using ProjectWarehouse.Server.Models.Roles;
using ProjectWarehouse.Server.Services;

namespace ProjectWarehouse.Server.Controllers;

[Route("api/roles")]
public class RolesController(
    RoleManager<ApplicationRole> roleManager,
    ApplicationDbContext db,
    IPermissionService permissionService) : AppControllerBase
{
    /// <summary>List all roles.</summary>
    [HttpGet]
    [Authorize(Policy = Permissions.Roles.View)]
    [ProducesResponseType<IReadOnlyList<RoleDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll()
    {
        var roles = await roleManager.Roles
            .Select(r => new RoleDto { Id = r.Id, Name = r.Name! })
            .ToListAsync();
        return Ok(roles);
    }

    /// <summary>Get a role by ID.</summary>
    [HttpGet("{id:guid}")]
    [Authorize(Policy = Permissions.Roles.View)]
    [ProducesResponseType<RoleDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id)
    {
        var role = await roleManager.FindByIdAsync(id.ToString());
        if (role is null)
            return NotFound(ErrorCode.RoleNotFound, "Role not found.");

        return Ok(new RoleDto { Id = role.Id, Name = role.Name! });
    }

    /// <summary>Create a new role.</summary>
    [HttpPost]
    [Authorize(Policy = Permissions.Roles.Create)]
    [ProducesResponseType<RoleDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create([FromBody] CreateRoleRequest request)
    {
        var existing = await roleManager.FindByNameAsync(request.Name);
        if (existing is not null)
            return ConflictField("name", ErrorCode.RoleAlreadyExists, "A role with this name already exists.");

        var role = new ApplicationRole { Name = request.Name };
        var result = await roleManager.CreateAsync(role);
        if (!result.Succeeded)
        {
            var errors = result.Errors.Select(e => ("root", ErrorCode.ValidationError, e.Description));
            return Problem(AppProblems.UnprocessableEntities(errors));
        }

        return CreatedAtAction(nameof(GetById), new { id = role.Id },
            new RoleDto { Id = role.Id, Name = role.Name! });
    }

    /// <summary>Update a role's name.</summary>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = Permissions.Roles.Edit)]
    [ProducesResponseType<RoleDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateRoleRequest request)
    {
        var role = await roleManager.FindByIdAsync(id.ToString());
        if (role is null)
            return NotFound(ErrorCode.RoleNotFound, "Role not found.");

        if (role.NormalizedName == "ADMIN")
            return Forbidden(ErrorCode.RoleProtected, "The Admin role cannot be modified.");

        role.Name = request.Name;
        var result = await roleManager.UpdateAsync(role);
        if (!result.Succeeded)
        {
            var errors = result.Errors.Select(e => ("root", ErrorCode.ValidationError, e.Description));
            return Problem(AppProblems.UnprocessableEntities(errors));
        }
        return Ok(new RoleDto { Id = role.Id, Name = role.Name! });
    }

    /// <summary>Delete a role.</summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = Permissions.Roles.Delete)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Delete(Guid id)
    {
        var role = await roleManager.FindByIdAsync(id.ToString());
        if (role is null)
            return NotFound(ErrorCode.RoleNotFound, "Role not found.");

        if (role.NormalizedName == "ADMIN")
            return Forbidden(ErrorCode.RoleProtected, "The Admin role cannot be deleted.");

        var result = await roleManager.DeleteAsync(role);
        if (!result.Succeeded)
        {
            var errors = result.Errors.Select(e => ("root", ErrorCode.ValidationError, e.Description));
            return Problem(AppProblems.UnprocessableEntities(errors));
        }
        return NoContent();
    }

    /// <summary>Get permissions assigned to a role.</summary>
    [HttpGet("{id:guid}/permissions")]
    [Authorize(Policy = Permissions.Roles.View)]
    [ProducesResponseType<IReadOnlyList<string>>(StatusCodes.Status200OK)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPermissions(Guid id)
    {
        var role = await roleManager.FindByIdAsync(id.ToString());
        if (role is null)
            return NotFound(ErrorCode.RoleNotFound, "Role not found.");

        var permissions = await db.RolePermissions
            .Where(rp => rp.RoleId == id)
            .Select(rp => rp.Permission)
            .ToListAsync();
        return Ok(permissions);
    }

    /// <summary>Assign a permission to a role.</summary>
    [HttpPost("{id:guid}/permissions")]
    [Authorize(Policy = Permissions.Roles.ManagePermissions)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AddPermission(Guid id, [FromBody] AssignRolePermissionRequest request)
    {
        var role = await roleManager.FindByIdAsync(id.ToString());
        if (role is null)
            return NotFound(ErrorCode.RoleNotFound, "Role not found.");

        if (!Permissions.All.Contains(request.Permission))
            return NotFound(ErrorCode.PermissionNotFound, $"Permission '{request.Permission}' does not exist.");

        var exists = await db.RolePermissions
            .AnyAsync(rp => rp.RoleId == id && rp.Permission == request.Permission);
        if (exists)
            return Conflict(ErrorCode.PermissionAlreadyAssigned, "Role already has this permission.");

        await permissionService.AddRolePermissionAsync(id, request.Permission);
        return NoContent();
    }

    /// <summary>Remove a permission from a role.</summary>
    [HttpDelete("{id:guid}/permissions/{permission}")]
    [Authorize(Policy = Permissions.Roles.ManagePermissions)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemovePermission(Guid id, string permission)
    {
        var role = await roleManager.FindByIdAsync(id.ToString());
        if (role is null)
            return NotFound(ErrorCode.RoleNotFound, "Role not found.");

        var exists = await db.RolePermissions
            .AnyAsync(rp => rp.RoleId == id && rp.Permission == permission);
        if (!exists)
            return NotFound(ErrorCode.PermissionNotFound, "Role does not have this permission.");

        await permissionService.RemoveRolePermissionAsync(id, permission);
        return NoContent();
    }
}
