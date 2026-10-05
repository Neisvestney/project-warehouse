namespace ProjectWarehouse.Server.Services;

/// <summary>
/// Carries a card's current mapping onto the lines of external orders imported through it, together with the
/// box components, returns and accruals that copy the line's catalog item.
/// </summary>
public interface IExternalOrderRebindService
{
    /// <summary>
    /// Binds the lines that were imported while their card had no mapping. Runs on every mapping change:
    /// filling a blank rewrites no history. Returns the number of lines bound.
    /// </summary>
    Task<int> BindUnmappedAsync(IReadOnlyCollection<Guid> cardIds, CancellationToken ct, TimeSpan? lockTimeout = null);

    /// <summary>
    /// The lines of the accounts' external orders dated from <paramref name="since"/> whose catalog item
    /// differs from their card's current mapping, ordered by order number.
    /// </summary>
    Task<IReadOnlyList<ExternalOrderRebindChange>> PlanRebindAsync(
        IReadOnlyCollection<Guid> accountIds, DateTime since, CancellationToken ct);

    /// <summary>
    /// Applies the changes whose line still holds <see cref="ExternalOrderRebindChange.OldCatalogItemId"/>
    /// and returns them; a line a concurrent rebind already moved is skipped.
    /// </summary>
    Task<IReadOnlyList<ExternalOrderRebindChange>> ApplyAsync(
        IReadOnlyList<ExternalOrderRebindChange> changes, CancellationToken ct, TimeSpan? lockTimeout = null);
}

public record ExternalOrderRebindChange(
    Guid ItemId,
    Guid OrderId,
    int OrderNumber,
    Guid CardId,
    Guid AccountId,
    int Quantity,
    Guid? OldCatalogItemId,
    Guid NewCatalogItemId);
