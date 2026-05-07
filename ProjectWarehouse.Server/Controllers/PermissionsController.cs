using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ProjectWarehouse.Server.Infrastructure;

namespace ProjectWarehouse.Server.Controllers;

[Route("api/permissions")]
public class PermissionsController : AppControllerBase
{
    /// <summary>Get all available static permissions defined in the system.</summary>
    [HttpGet]
    [Authorize]
    [ProducesResponseType<IReadOnlyList<string>>(StatusCodes.Status200OK)]
    public IActionResult GetAll() => Ok(Permissions.All);
}
