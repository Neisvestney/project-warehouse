using ProjectWarehouse.Server.Domain;
using ProjectWarehouse.Server.Infrastructure;

namespace ProjectWarehouse.Server.Models.Stocktakes;

/// <summary>
/// Preview of what finishing the document would do. Produced by the same calculator the finish
/// operation runs, so the two can never disagree.
/// </summary>
public class StocktakeDifferencesDto
{
    public IReadOnlyList<StocktakeNodeDifferencesDto> Nodes { get; init; } = [];
    public int TotalSurplusQuantity { get; init; }
    public int TotalShortageQuantity { get; init; }
    public int TotalRelocations { get; init; }
    public bool HasDifferences { get; init; }

    /// <summary>Blockers. While this is non-empty the document cannot be finished.</summary>
    public IReadOnlyList<StocktakeProblemDto> Problems { get; init; } = [];
}

public class StocktakeProblemDto
{
    public Guid StoragePlaceNodeId { get; init; }
    public ErrorCode Code { get; init; }
    public string Message { get; init; } = null!;
}

public class StocktakeNodeDifferencesDto
{
    public Guid StoragePlaceNodeId { get; init; }
    public string[] NodePath { get; init; } = [];
    public IReadOnlyList<StocktakeDifferenceLineDto> Lines { get; init; } = [];
}

public class StocktakeDifferenceLineDto
{
    public StocktakeItemKind Kind { get; init; }
    public Guid CatalogItemId { get; init; }
    public string CatalogItemName { get; init; } = null!;
    public string? InventoryNumber { get; init; }
    public int Expected { get; init; }
    public int Counted { get; init; }
    public int Delta { get; init; }
    public StocktakeDifferenceResolution Resolution { get; init; }

    /// <summary>Stock sitting in the cell that the document says nothing about — it will be written off.</summary>
    public bool MissingFromDocument { get; init; }

    /// <summary>Where the serial currently lives. Set only for <see cref="StocktakeDifferenceResolution.Relocation"/>.</summary>
    public Guid? CurrentNodeId { get; init; }
    public string[]? CurrentNodePath { get; init; }
}
