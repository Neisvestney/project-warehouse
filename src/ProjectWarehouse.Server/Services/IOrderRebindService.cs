using ProjectWarehouse.Server.Domain;
using ProjectWarehouse.Server.Models;

namespace ProjectWarehouse.Server.Services;

/// <summary>
/// Carries a card's current mapping onto the lines of orders imported through it, together with the box
/// components, returns and accruals that copy the line's catalog item. External orders follow the mapping
/// automatically or by an explicit history rebind; a working FBS order only on request, before assembly.
/// </summary>
public interface IOrderRebindService
{
    /// <summary>
    /// Rebinds the lines of working FBS orders in <see cref="OrderStatus.Confirmed"/> onto their cards' current
    /// mapping. The caller holds the orders' locks. An order that is not such an order, or has a line whose
    /// card is unmapped, is reported in <see cref="WorkingOrdersRebindResult.Failures"/> and left untouched.
    /// </summary>
    Task<WorkingOrdersRebindResult> RebindWorkingOrdersAsync(IReadOnlyCollection<Order> orders, CancellationToken ct);

    /// <summary>
    /// Binds the lines that were imported while their card had no mapping. Runs on every mapping change:
    /// filling a blank rewrites no history. Returns the number of lines bound.
    /// </summary>
    Task<int> BindUnmappedAsync(IReadOnlyCollection<Guid> cardIds, CancellationToken ct, TimeSpan? lockTimeout = null);

    /// <summary>
    /// The lines of the accounts' external orders dated from <paramref name="since"/> whose catalog item
    /// differs from their card's current mapping, ordered by order number.
    /// </summary>
    Task<IReadOnlyList<OrderRebindChange>> PlanRebindAsync(
        IReadOnlyCollection<Guid> accountIds, DateTime since, CancellationToken ct);

    /// <summary>
    /// Applies the changes whose line still holds <see cref="OrderRebindChange.OldCatalogItemId"/>
    /// and returns them; a line a concurrent rebind already moved is skipped.
    /// </summary>
    Task<IReadOnlyList<OrderRebindChange>> ApplyAsync(
        IReadOnlyList<OrderRebindChange> changes, CancellationToken ct, TimeSpan? lockTimeout = null);
}

public record OrderRebindChange(
    Guid ItemId,
    Guid OrderId,
    int OrderNumber,
    Guid CardId,
    Guid AccountId,
    int Quantity,
    Guid? OldCatalogItemId,
    Guid NewCatalogItemId);

/// <param name="ReboundOrderIds">Orders with at least one line moved.</param>
/// <param name="Failures">Orders refused, by id.</param>
public record WorkingOrdersRebindResult(
    IReadOnlyList<Guid> ReboundOrderIds,
    IReadOnlyDictionary<Guid, AppFieldError> Failures);
