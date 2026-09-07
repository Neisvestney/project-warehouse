using ProjectWarehouse.Server.Infrastructure;

namespace ProjectWarehouse.Server.Models.Statistics;

/// <summary>A saved column layout of the movement report. Shared — every viewer sees the same list.</summary>
public class StockMovementReportPresetDto : IHasIdentity
{
    public Guid Id { get; init; }
    public string Name { get; init; } = null!;
    public bool IsDefault { get; init; }

    /// <summary>Sub-columns in display order.</summary>
    public IReadOnlyList<StockMovementMetricDto> Metrics { get; init; } = [];

    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
    public Guid? UpdatedById { get; init; }
    public string? UpdatedByName { get; init; }

    /// <summary>Optimistic concurrency token — pass it back on update, the preset is shared.</summary>
    public uint Version { get; init; }
}
