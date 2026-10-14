using ProjectWarehouse.Server.Domain;

namespace ProjectWarehouse.Server.Models.Orders;

/// <summary>Aggregates of the whole filtered order list, shown above the table when nothing is selected.</summary>
public class OrderListMetaDto
{
    /// <summary>Total quantity of box components across the filtered orders.</summary>
    public int ComponentCount { get; init; }

    /// <summary>
    /// Orders with a marketplace line not mapped to the catalog — what keeps an FBS draft from going to work.
    /// Counted only for the FBS drafts list (<c>type=fbs</c>, <c>status=draft</c>), 0 for any other.
    /// </summary>
    public int UnmappedOrderCount { get; init; }

    /// <summary>
    /// Orders past their planned shipment date that are not assembled yet. Ignores the <c>overdue</c> filter so
    /// both counters stay visible while one of them is applied.
    /// </summary>
    public int OverdueAssemblyCount { get; init; }

    /// <summary>
    /// Orders past their planned shipment date that are assembled but not shipped. Ignores the <c>overdue</c>
    /// filter so both counters stay visible while one of them is applied.
    /// </summary>
    public int OverdueShipmentCount { get; init; }

    /// <summary>
    /// Order count per status, one entry for every status. Ignores the <c>status</c> filter, so the list tabs
    /// show what each of them would hold.
    /// </summary>
    public IReadOnlyList<StatusCountDto<OrderStatus>> StatusCounts { get; init; } = [];
}
