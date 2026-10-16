using System.Security.Claims;
using ProjectWarehouse.Server.Models;
using ProjectWarehouse.Server.Models.Analytics;

namespace ProjectWarehouse.Server.Services;

/// <summary>
/// The «Выплаты маркетплейсов» page: money the marketplaces owe for postings in transit and delivered but not
/// yet accrued, and what the accrual journal credited in the period. Takes the caller for the shared pipeline but
/// checks no permission beyond the controller's <c>analytics.view</c>.
/// </summary>
/// <remarks>Throws <see cref="Infrastructure.ValidationException"/> on an inverted, too long or one-ended period.</remarks>
public interface IAnalyticsPayoutsService
{
    Task<PayoutsDto> GetPayoutsAsync(ClaimsPrincipal user, PayoutsRequest request, CancellationToken ct = default);

    /// <remarks>Also throws on a day step over all time longer than the period limit.</remarks>
    Task<PayoutsTimeseriesDto> GetTimeseriesAsync(
        ClaimsPrincipal user, PayoutsTimeseriesRequest request, CancellationToken ct = default);

    /// <summary>The postings behind one debt bucket, oldest first.</summary>
    Task<Paginated<PayoutsPostingDto>> GetPostingsAsync(
        ClaimsPrincipal user, PayoutsPostingsRequest request, CancellationToken ct = default);
}
