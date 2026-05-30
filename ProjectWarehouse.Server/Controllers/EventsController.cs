using AutoMapper;
using Microsoft.AspNetCore.Components;
using ProjectWarehouse.Server.Data;

namespace ProjectWarehouse.Server.Controllers;

[Route("api/events")]
public class EventsController(ApplicationDbContext db, IMapper mapper) : AppControllerBase
{
    
}