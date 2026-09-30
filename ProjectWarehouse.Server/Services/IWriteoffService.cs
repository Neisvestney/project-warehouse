using ProjectWarehouse.Server.Domain;
using ProjectWarehouse.Server.Models;
using ProjectWarehouse.Server.Models.Writeoffs;

namespace ProjectWarehouse.Server.Services;

public interface IWriteoffService
{
    /// <summary>Adds the detail Include chain to <paramref name="source"/>; items only when asked.</summary>
    IQueryable<Writeoff> WithDetails(IQueryable<Writeoff> source, bool includeItems = false);

    /// <summary>
    /// Validates and applies a status transition and saves it; finishing removes the stock in one transaction.
    /// The write-off must be loaded with items. Null on success, otherwise the problem to return; nothing is
    /// saved then.
    /// </summary>
    Task<AppProblemDetails?> TransitionAsync(Writeoff writeoff, WriteoffTransition transition, CancellationToken ct = default);
}
