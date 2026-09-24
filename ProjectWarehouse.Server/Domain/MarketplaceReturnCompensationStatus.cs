namespace ProjectWarehouse.Server.Domain;

/// <summary>Mirrors Ozon's own compensation status ids, so the values double as the API filter.</summary>
public enum MarketplaceReturnCompensationStatus
{
    Sent = 1,
    Received = 2,
    Canceled = 3,
    DecompensationSent = 4,
}
