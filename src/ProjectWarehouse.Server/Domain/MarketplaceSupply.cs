using EntityFrameworkCore.Projectables;
using ProjectWarehouse.Server.Infrastructure;

namespace ProjectWarehouse.Server.Domain;

/// <summary>
/// A marketplace's batch of FBS postings handed over to delivery together — the Wildberries supply. Owned by the
/// marketplace: the sync creates and updates it, WMS never edits it. A posting may move between open supplies, so
/// <see cref="MarketplaceOrder.SupplyId"/> follows the marketplace on every sync.
/// </summary>
public class MarketplaceSupply : IHasIdentity
{
    public Guid Id { get; set; }

    public Guid MarketplaceAccountId { get; set; }
    public MarketplaceAccount MarketplaceAccount { get; set; } = null!;

    public string ExternalId { get; set; } = null!;

    /// <summary>Seller-given name; the marketplace allows it to be empty.</summary>
    public string? Name { get; set; }

    public DateTime CreatedAt { get; set; }

    /// <summary>When the supply was handed over to delivery. Null while it is open.</summary>
    public DateTime? ClosedAt { get; set; }

    /// <summary>Closed on the marketplace: its composition can no longer change.</summary>
    public bool IsDone { get; set; }

    /// <summary>
    /// When the composition was last read. A done supply is read once more after it closes and never again.
    /// </summary>
    public DateTime? OrdersSyncedAt { get; set; }

    public ICollection<MarketplaceOrder> Orders { get; set; } = [];

    [Projectable]
    public string SearchString => ExternalId + " " + (Name ?? "");
}
