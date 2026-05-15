using AutoMapper;
using AutoMapper.QueryableExtensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProjectWarehouse.Server.Data;
using ProjectWarehouse.Server.Infrastructure;
using ProjectWarehouse.Server.Models;

namespace ProjectWarehouse.Server.Controllers;

[Route("api/homepagecontent")]
public class HomePageContentController(ApplicationDbContext db, IMapper mapper) : AppControllerBase
{
    /// <summary>Get list of AppEntities for home page.</summary>
    [HttpGet]
    [Authorize]
    [ProducesResponseType<IReadOnlyList<AppEntity>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetHomePageContent()
    {
        var list = new List<AppEntity>();

        var userCanViewAllWarehouses = User.HasClaim("permission", Permissions.Warehouses.View);

        if (userCanViewAllWarehouses)
        {
            var warehouses = await db.Warehouses.ProjectTo<AppEntity>(mapper.ConfigurationProvider).Take(2).ToListAsync();
            list.AddRange(warehouses);
        }

        return Ok(list);
    }
}