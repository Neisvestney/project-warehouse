using ProjectWarehouse.Server.Domain;
using ProjectWarehouse.Server.Integrations.Abstractions;

namespace ProjectWarehouse.Server.Models.Integrations;

/// <summary>
/// An account offered in the "sync orders" dialog.
/// </summary>
/// <remarks>
/// Accounts with unmapped cards or warehouses are still listed — the counts drive a warning next to
/// the checkbox, which tells the user far more than a silently missing row.
/// </remarks>
public class MarketplaceOrderSyncTargetDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = null!;
    public MarketplaceType Type { get; init; }
    public MarketplaceCapabilities Capabilities { get; set; }
    public bool CredentialsUnreadable { get; set; }
    public bool IsSyncRunning { get; init; }

    public int MappedWarehouseCount { get; init; }
    public int UnmappedWarehouseCount { get; init; }
    public int UnmappedCardCount { get; init; }
}
