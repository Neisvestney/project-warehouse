using ProjectWarehouse.Server.Domain;

namespace ProjectWarehouse.Server.Models.Statistics;

public class StockMovementCellDto
{
    /// <summary>Newest first; a group sits where its latest movement would.</summary>
    public IReadOnlyList<StockMovementEntryDto> Entries { get; init; } = [];

    /// <summary>Movements in the whole cell; sent with the first page only.</summary>
    public int? TotalCount { get; init; }

    /// <summary>Pass back as <c>before</c> to load the next, older page; null on the last one.</summary>
    public DateTime? NextBefore { get; init; }
}

/// <summary>
/// Either a single movement or a burst of them: more than the threshold, made by one user with one action
/// and direction, with no pause between neighbours longer than the gap. Batch assembly of a hundred FBS
/// orders is the case it exists for.
/// </summary>
public class StockMovementEntryDto
{
    public bool IsGroup { get; init; }
    public DateTime FirstAt { get; init; }
    public DateTime LastAt { get; init; }
    public StockMovementDirection Direction { get; init; }
    public string Action { get; init; } = null!;
    public Guid? UserId { get; init; }
    public string? UserName { get; init; }
    public int Quantity { get; init; }

    /// <summary>Exactly one for a single movement; newest first.</summary>
    public IReadOnlyList<StockMovementCellRowDto> Movements { get; init; } = [];
}

public class StockMovementCellRowDto : StockMovementDto
{
    /// <summary>
    /// How much of <see cref="StockMovementDto.Quantity"/> the pivot's same-day netting takes out of the
    /// cell; zero for a row it counts in full.
    /// </summary>
    public int NettedQuantity { get; set; }

    /// <summary>
    /// Halves of this row's cancellation pairs that the cell's metric does not cover and that would
    /// otherwise be missing from the list. Each hangs under exactly one row; a half the metric covers is
    /// listed as a row of its own instead.
    /// </summary>
    public IReadOnlyList<StockMovementCellRowDto> Counterparts { get; set; } = [];
}
