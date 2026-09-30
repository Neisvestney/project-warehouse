using ProjectWarehouse.Server.Domain;
using ProjectWarehouse.Server.Models;
using ProjectWarehouse.Server.Models.Receipts;

namespace ProjectWarehouse.Server.Services;

public interface IReceiptService
{
    /// <summary>Adds the detail Include chain to <paramref name="source"/>; items and placements only when asked.</summary>
    IQueryable<Receipt> WithDetails(IQueryable<Receipt> source, bool includeItems = false);

    /// <summary>
    /// Validates and applies a status transition and saves it. The receipt must be loaded with items and
    /// placements. Null on success, otherwise the problem to return; nothing is saved then.
    /// </summary>
    Task<AppProblemDetails?> TransitionAsync(Receipt receipt, ReceiptTransition transition, CancellationToken ct = default);
}
