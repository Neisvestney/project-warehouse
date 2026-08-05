namespace ProjectWarehouse.Server.Domain;

public enum MarketplaceSyncStatus
{
    Running = 0,
    Success = 1,
    Failed = 2,

    /// <summary>Reserved: cancelling a running sync is not offered in the first version.</summary>
    Canceled = 3,
}
