using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using ProjectWarehouse.Server.Domain;

namespace ProjectWarehouse.Server.Models.Statistics;

/// <summary>
/// One sub-column of the pivot. Every predicate is optional and they combine with AND; leaving all of
/// them empty is legal and means «every movement». A non-empty document tag list keeps only movements
/// made by a document of that type carrying any of the tags.
/// </summary>
public class StockMovementMetricDto
{
    [JsonRequired]
    [Required, MaxLength(60)]
    public string Name { get; init; } = null!;

    public string[]? Actions { get; init; }

    public StockMovementDirection[]? Directions { get; init; }

    public Guid[]? ReceiptTagIds { get; init; }

    public Guid[]? OrderTagIds { get; init; }

    public Guid[]? WriteoffTagIds { get; init; }

    public Guid[]? StocktakeTagIds { get; init; }
}
