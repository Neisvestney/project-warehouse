namespace ProjectWarehouse.Server.Models.Analytics;

public enum AnalyticsChannelKind
{
    Marketplace = 0,

    /// <summary>Every Direct order, counted by the orders themselves rather than as a sum of tag rows.</summary>
    Direct = 1,

    /// <summary>Direct orders carrying one tag; an order with several tags is in each of their rows.</summary>
    DirectTag = 2,

    DirectUntagged = 3,
}
