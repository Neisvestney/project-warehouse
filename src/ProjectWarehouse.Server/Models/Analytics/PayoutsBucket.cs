namespace ProjectWarehouse.Server.Models.Analytics;

/// <summary>A debt bucket of the payouts page; <see cref="InTransitOverdue"/> is a part of <see cref="InTransit"/>.</summary>
public enum PayoutsBucket
{
    InTransit = 0,
    InTransitOverdue = 1,
    DeliveredNotAccrued = 2,
    NotAccruedByMarketplace = 3,
}
