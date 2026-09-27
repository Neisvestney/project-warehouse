using ProjectWarehouse.Server.Models.Analytics;

namespace ProjectWarehouse.Server.Services;

/// <summary>The single row of analytics parameters, shared by the whole system.</summary>
public interface IAnalyticsSettingsService
{
    /// <summary>The values reports compute with: stored ones, defaults where nothing is stored.</summary>
    Task<AnalyticsOptions> GetOptionsAsync(CancellationToken ct = default);

    Task<AnalyticsSettingsDto> GetSettingsAsync(CancellationToken ct = default);

    /// <exception cref="Infrastructure.ValidationException">A value breaks the order of a boundary pair or the age list rules.</exception>
    /// <exception cref="Infrastructure.AnalyticsSettingsConflictException">Someone else saved since <c>request.Version</c>.</exception>
    Task<(AnalyticsSettingsDto Before, AnalyticsSettingsDto After)> UpdateSettingsAsync(
        UpdateAnalyticsSettingsRequest request, Guid? actorId, CancellationToken ct = default);
}
