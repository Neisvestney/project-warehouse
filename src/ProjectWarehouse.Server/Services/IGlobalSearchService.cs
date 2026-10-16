using System.Security.Claims;
using ProjectWarehouse.Server.Models;

namespace ProjectWarehouse.Server.Services;

public interface IGlobalSearchService
{
    Task<List<AppEntity>> SearchAsync(ClaimsPrincipal user, string searchString, CancellationToken ct = default);
}
