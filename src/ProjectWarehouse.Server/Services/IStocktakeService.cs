using ProjectWarehouse.Server.Domain;
using ProjectWarehouse.Server.Models;
using ProjectWarehouse.Server.Models.Stocktakes;

namespace ProjectWarehouse.Server.Services;

public interface IStocktakeService
{
    /// <summary>Adds the detail Include chain to <paramref name="source"/>; counted items only when asked.</summary>
    IQueryable<Stocktake> WithDetails(IQueryable<Stocktake> source, bool includeItems = false);

    /// <summary>
    /// Two counts running over one cell would fight each other at finish — the second one to apply
    /// overwrites the first with quantities measured before it. Checked wherever a cell can end up
    /// in a running count: at start, and when the scope of an InProgress document grows.
    /// Row-locks the cells until the caller's transaction ends, so it must run inside one.
    /// </summary>
    Task<AppProblemDetails?> FindNodeCountedElsewhereAsync(
        Guid stocktakeId, IReadOnlyCollection<Guid> nodeIds, CancellationToken ct = default);

    /// <summary>
    /// Validates and applies a status transition and saves it; finishing applies the count to stock in one
    /// transaction. The stocktake must be loaded with items. Null on success, otherwise the problem to return;
    /// nothing is saved then.
    /// </summary>
    Task<AppProblemDetails?> TransitionAsync(Stocktake stocktake, StocktakeTransition transition, CancellationToken ct = default);
}
