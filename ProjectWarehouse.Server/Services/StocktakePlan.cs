using ProjectWarehouse.Server.Domain;
using ProjectWarehouse.Server.Infrastructure;
using ProjectWarehouse.Server.Models.Stocktakes;

namespace ProjectWarehouse.Server.Services;

/// <summary>
/// Everything finishing a stocktake would do, computed without touching stock. The preview endpoint
/// renders it and the finish endpoint applies it, so the two can never disagree.
/// </summary>
public class StocktakePlan
{
    public required IReadOnlyList<StocktakePlanLine> Lines { get; init; }

    /// <summary>Lines that cannot be applied. Non-empty means finishing must be refused.</summary>
    public required IReadOnlyList<StocktakePlanProblem> Problems { get; init; }

    public required IReadOnlyDictionary<Guid, string[]> NodePaths { get; init; }
}

public class StocktakePlanLine
{
    public required Guid StoragePlaceNodeId { get; init; }

    /// <summary>The document line this came from. Null for stock the document does not mention.</summary>
    public required Guid? StocktakeItemId { get; init; }

    public required StocktakeItemKind Kind { get; init; }
    public required Guid CatalogItemId { get; init; }
    public required string CatalogItemName { get; init; }
    public string? InventoryNumber { get; init; }
    public Guid? UnitInventoryItemId { get; init; }

    public required int Expected { get; init; }
    public required int Counted { get; init; }

    /// <summary>Change this line applies to the counted cell.</summary>
    public required int Delta { get; init; }

    public required StocktakeDifferenceResolution Resolution { get; init; }

    /// <summary>Where the serial currently sits. Set for <see cref="StocktakeDifferenceResolution.Relocation"/>.</summary>
    public Guid? CurrentNodeId { get; init; }

    public bool MissingFromDocument => StocktakeItemId is null;
}

public class StocktakePlanProblem
{
    public required Guid StoragePlaceNodeId { get; init; }
    public required Guid? StocktakeItemId { get; init; }
    public required ErrorCode Code { get; init; }
    public required string Message { get; init; }
}
