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

    /// <summary>
    /// The class of each item of the table's filters in a rolling window at the end of every month of the year up to
    /// the period's end, or up to yesterday when the period is not over.
    /// </summary>
    Task<AbcTimelineDto> GetTimelineAsync(ClaimsPrincipal user, AbcFilterRequest request, CancellationToken ct = default);
}
