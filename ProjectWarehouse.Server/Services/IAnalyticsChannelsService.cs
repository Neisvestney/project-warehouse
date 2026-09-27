using System.Security.Claims;
using ProjectWarehouse.Server.Models.Analytics;

namespace ProjectWarehouse.Server.Services;

/// <summary>
/// The «Сводка по каналам» page, derived from orders and marketplace returns at request time. Methods take
/// the caller for the shared pipeline but check no permission beyond the controller's <c>analytics.view</c>:
/// shops belong to the company, not to a warehouse.
/// </summary>
/// <remarks>Every method throws <see cref="Infrastructure.ValidationException"/> on an inverted or too long period.</remarks>
public interface IAnalyticsChannelsService
{
    Task<ChannelsSummaryDto> GetSummaryAsync(
        ClaimsPrincipal user, ChannelsSummaryRequest request, CancellationToken ct = default);

    Task<ChannelsTimeseriesDto> GetTimeseriesAsync(
        ClaimsPrincipal user, ChannelsTimeseriesRequest request, CancellationToken ct = default);

    Task<ChannelsReturnsDto> GetReturnsAsync(
        ClaimsPrincipal user, ChannelsReturnsRequest request, CancellationToken ct = default);

    Task<ChannelsCancellationsDto> GetCancellationsAsync(
        ClaimsPrincipal user, ChannelsCancellationsRequest request, CancellationToken ct = default);

    Task<ChannelsLossesDto> GetLossesAsync(
        ClaimsPrincipal user, ChannelsLossesRequest request, CancellationToken ct = default);

    Task<ChannelsTopItemsDto> GetTopItemsAsync(
        ClaimsPrincipal user, ChannelsTopItemsRequest request, CancellationToken ct = default);

    Task<ChannelsWeekdaysDto> GetWeekdaysAsync(
        ClaimsPrincipal user, ChannelsWeekdaysRequest request, CancellationToken ct = default);
}
