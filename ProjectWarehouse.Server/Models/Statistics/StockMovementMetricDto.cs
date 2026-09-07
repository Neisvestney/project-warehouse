using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using ProjectWarehouse.Server.Domain;

namespace ProjectWarehouse.Server.Models.Statistics;

/// <summary>
/// One sub-column of the pivot. All three predicates are optional and combine with AND; leaving every
/// one of them empty is legal and means «every movement».
/// </summary>
public class StockMovementMetricDto
{
    [JsonRequired]
    [Required, MaxLength(60)]
    public string Name { get; init; } = null!;

    public string[]? Actions { get; init; }

    public StockMovementDirection[]? Directions { get; init; }

    public Guid[]? ReceiptTagIds { get; init; }
}
