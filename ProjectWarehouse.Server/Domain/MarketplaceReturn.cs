using ProjectWarehouse.Server.Infrastructure;

namespace ProjectWarehouse.Server.Domain;

/// <summary>
/// One returned unit as the marketplace reports it — a posting that sent back nine of a product yields
/// nine rows, so metrics sum <see cref="Quantity"/> rather than aggregate here. Never deleted; every
/// marketplace field is overwritten by each sync that sees it. Warehouse stock is not touched.
/// </summary>
public class MarketplaceReturn : IHasIdentity
{
    public Guid Id { get; set; }

    public Guid MarketplaceAccountId { get; set; }
    public MarketplaceAccount MarketplaceAccount { get; set; } = null!;

    public string ExternalId { get; set; } = null!;

    /// <summary>Always stored, so a return that arrived before its posting can be linked later.</summary>
    public string PostingNumber { get; set; } = null!;

    /// <summary>Null until the posting is imported. Once set it is never reconsidered.</summary>
    public Guid? OrderId { get; set; }
    public Order? Order { get; set; }

    public Guid? OrderMarketplaceItemId { get; set; }
    public OrderMarketplaceItem? OrderMarketplaceItem { get; set; }

    /// <summary>
    /// Copied from the matched <see cref="Domain.OrderMarketplaceItem.CatalogItemId"/>, not from the live card
    /// mapping, so a return always lands on the same catalog item as the sale it reverses.
    /// </summary>
    public Guid? CatalogItemId { get; set; }
    public CatalogItem? CatalogItem { get; set; }

    public string? Sku { get; set; }
    public string OfferId { get; set; } = null!;

    public MarketplaceReturnScheme Scheme { get; set; }

    // diagnostics only
    public string? RawScheme { get; set; }

    public MarketplaceReturnKind Kind { get; set; }

    // diagnostics only — the normalized Kind is what queries use
    public string? RawKind { get; set; }

    public int Quantity { get; set; }

    /// <summary>Unit price for the buyer.</summary>
    public decimal? Price { get; set; }
    public string? CurrencyCode { get; set; }

    /// <summary>Return reason as worded by the marketplace.</summary>
    public string? Reason { get; set; }

    // The raw vocabulary is long and keeps growing, so it is not mapped; only IsCancelled is derived from it
    public string? RawStatus { get; set; }
    public string? StatusName { get; set; }
    public DateTime? StatusChangedAt { get; set; }

    /// <summary>
    /// The request was cancelled or rejected and the item stayed with the buyer — the one thing the raw status
    /// says that metrics need. Recomputed from each status the marketplace reports, so a reopened dispute clears it.
    /// </summary>
    public bool IsCancelled { get; set; }

    /// <summary>When the buyer handed the item back or refused it.</summary>
    public DateTime? ReturnedAt { get; set; }

    /// <summary>When the item reached the marketplace warehouse or was handed to the seller.</summary>
    public DateTime? FinalAt { get; set; }

    /// <summary>Null when no compensation was ever raised.</summary>
    public MarketplaceReturnCompensationStatus? CompensationStatus { get; set; }
    public DateTime? CompensationStatusChangedAt { get; set; }

    // diagnostics only
    public string? SourceExternalId { get; set; }
    public string? ExemplarId { get; set; }

    public DateTime SyncedAt { get; set; }
}
