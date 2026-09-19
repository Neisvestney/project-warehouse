namespace ProjectWarehouse.Server.Models.Orders;

/// <summary>Aggregates of the whole filtered order list, shown above the table when nothing is selected.</summary>
public class OrderListMetaDto
{
    /// <summary>Total quantity of box components across the filtered orders.</summary>
    public int ComponentCount { get; init; }

    /// <summary>Orders past their planned shipment date that are neither shipped nor canceled.</summary>
    public int OverdueCount { get; init; }
}
