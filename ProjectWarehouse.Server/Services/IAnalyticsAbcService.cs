using System.Security.Claims;
using ProjectWarehouse.Server.Models.Analytics;

namespace ProjectWarehouse.Server.Services;

/// <summary>
/// The «ABC / XYZ» page: catalog items ranked by their share of the period's sales and classed by how steady
/// their weekly or monthly demand is. Takes the caller for the shared pipeline but checks no permission beyond
/// the controller's <c>analytics.view</c>.
/// </summary>
/// <remarks>Throws <see cref="Infrastructure.ValidationException"/> on an inverted or too long period.</remarks>
public interface IAnalyticsAbcService
{
    Task<AbcDto> GetAbcAsync(ClaimsPrincipal user, AbcRequest request, CancellationToken ct = default);
}
