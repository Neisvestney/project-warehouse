using ProjectWarehouse.Server.Infrastructure;

namespace ProjectWarehouse.Server.Models.Analytics;

public class AnalyticsSettingsDto : IHasIdentity
{
    public Guid Id { get; init; }

    public AnalyticsSettingsValuesDto Saved { get; init; } = null!;

    /// <summary>System constants, so the form can show them as placeholders.</summary>
    public AnalyticsOptions Defaults { get; init; } = null!;

    /// <summary>What the reports will actually use.</summary>
    public AnalyticsOptions Effective { get; init; } = null!;

    /// <summary>Row version to send back with the update.</summary>
    public uint Version { get; init; }

    public DateTime? UpdatedAt { get; init; }
    public string? UpdatedByName { get; init; }
}
