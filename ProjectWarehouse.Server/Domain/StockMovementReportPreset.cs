using System.ComponentModel.DataAnnotations.Schema;
using ProjectWarehouse.Server.Infrastructure;

namespace ProjectWarehouse.Server.Domain;

/// <summary>
/// A saved column layout of the stock movement report, shared by every user who can view the report.
/// The order of <see cref="Metrics"/> is the order of the sub-columns; a metric that should not be shown
/// is absent from the list rather than flagged.
/// </summary>
public class StockMovementReportPreset : IHasIdentity
{
    public Guid Id { get; set; }

    public string Name { get; set; } = null!;

    /// <summary>At most one preset carries it — the one the page opens with.</summary>
    public bool IsDefault { get; set; }

    [Column(TypeName = "jsonb")]
    public List<StockMovementMetric> Metrics { get; set; } = [];

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public Guid? UpdatedById { get; set; }
    public ApplicationUser? UpdatedBy { get; set; }
}
