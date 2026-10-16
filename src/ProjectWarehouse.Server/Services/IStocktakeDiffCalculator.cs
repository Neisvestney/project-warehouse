using ProjectWarehouse.Server.Domain;
using ProjectWarehouse.Server.Models.Stocktakes;

namespace ProjectWarehouse.Server.Services;

public interface IStocktakeDiffCalculator
{
    /// <summary>
    /// Compares the counted lines against live stock. Reads only — safe to call outside a transaction.
    /// The stocktake must be loaded with Nodes, their StoragePlaceNode and Items.
    /// </summary>
    Task<StocktakePlan> BuildPlanAsync(Stocktake stocktake, CancellationToken ct = default);

    StocktakeDifferencesDto ToDto(StocktakePlan plan);
}
