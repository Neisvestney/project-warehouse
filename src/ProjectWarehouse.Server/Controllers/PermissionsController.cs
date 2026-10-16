using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ProjectWarehouse.Server.Infrastructure;

namespace ProjectWarehouse.Server.Controllers;

[Route("api/permissions")]
public class PermissionsController : AppControllerBase
{
    /// <summary>Get all available static permissions defined in the system.</summary>
    /// <remarks>
    /// Requires authentication only — the list is the same for everyone and says nothing about the caller's
    /// own rights. It is the source of the strings accepted by <c>PUT /api/roles</c> and
    /// <c>PUT /api/users/{id}</c>; anything outside it is rejected there with <c>permissionNotFound</c>.
    /// No error codes.
    /// </remarks>
    [HttpGet]
    [Authorize]
    [ProducesResponseType<IReadOnlyList<string>>(StatusCodes.Status200OK)]
    public IActionResult GetAll() => Ok(Permissions.All);
}
