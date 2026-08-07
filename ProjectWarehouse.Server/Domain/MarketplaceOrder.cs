using System.ComponentModel.DataAnnotations.Schema;
using ProjectWarehouse.Server.Models;

namespace ProjectWarehouse.Server.Domain;

/// <summary>
/// Marketplace extension of an <see cref="Order"/>, 1:1 on a shared primary key.
/// </summary>
/// <remarks>
/// Deliberately not <c>IHasIdentity</c>: the entity is never addressed over HTTP, never appears in a
/// list, and its change history is kept on the order. It also holds the one thing the orders domain had
/// no room for — which account's credentials to use when fetching a label.
/// </remarks>
public class MarketplaceOrder
{
    public Guid OrderId { get; set; }
    public Order Order { get; set; } = null!;

    public Guid MarketplaceAccountId { get; set; }
    public MarketplaceAccount MarketplaceAccount { get; set; } = null!;

    public string PostingNumber { get; set; } = null!;

    /// <summary>Number of the marketplace order this posting belongs to.</summary>
    public string? ExternalOrderNumber { get; set; }

    public MarketplaceOrderStatus Status { get; set; }

    // diagnostics only — the normalized Status is what the UI and queries use
    public string? RawStatus { get; set; }
    public string? RawSubstatus { get; set; }

    /// <summary>Deadline the marketplace expects the posting to be packed by.</summary>
    public DateTime? ShipmentDate { get; set; }

    public DateTime? InProcessAt { get; set; }
    public string? TrackingNumber { get; set; }
    public string? DeliveryMethodName { get; set; }

    /// <summary>How many packages the marketplace expects. A hint for the packer, not a box count in WMS.</summary>
    public int MultiBoxQty { get; set; } = 1;

    public Guid? LabelFileId { get; set; }
    public DataFile? LabelFile { get; set; }
    public DateTime? LabelFetchedAt { get; set; }

    [Column(TypeName = "jsonb")] public AppFieldError? LabelError { get; set; }

    public DateTime StatusSyncedAt { get; set; }
    public DateTime SyncedAt { get; set; }
}
