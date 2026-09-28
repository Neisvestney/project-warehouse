using ProjectWarehouse.Server.Infrastructure;

namespace ProjectWarehouse.Server.Domain;

/// <summary>
/// One money movement of a marketplace accrual. The marketplace groups several movements under one accrual —
/// a sale carries its price, its commission and its delivery services — and each becomes a row here, so
/// Σ <see cref="Amount"/> over an order is what the seller was actually paid for it. The rows of an accrual are
/// replaced as a whole whenever the marketplace reports it differently.
/// </summary>
public class MarketplaceAccrual : IHasIdentity
{
    public Guid Id { get; set; }

    public Guid MarketplaceAccountId { get; set; }
    public MarketplaceAccount MarketplaceAccount { get; set; } = null!;

    /// <summary>Id of the marketplace accrual; its rows share it and are told apart by <see cref="LineNo"/>.</summary>
    public string ExternalId { get; set; } = null!;

    public int LineNo { get; set; }

    /// <summary>The marketplace's own accounting day, not a moment in time.</summary>
    public DateOnly Date { get; set; }

    public MarketplaceAccrualScope Scope { get; set; }

    public MarketplaceAccrualCategory Category { get; set; }

    // the marketplace's type id; Category is what queries use
    public string RawTypeId { get; set; } = null!;

    /// <summary>
    /// Posting, order or service number as the marketplace reports it. An Ozon item fee may name the order
    /// rather than the posting. Always stored, so an accrual that arrived before its posting can be linked later.
    /// </summary>
    public string? UnitNumber { get; set; }

    public string? Sku { get; set; }

    /// <summary>Signed: what the seller receives is positive, what the marketplace keeps is negative.</summary>
    public decimal Amount { get; set; }

    public string? CurrencyCode { get; set; }

    /// <summary>Null until the posting is imported, and always for an accrual about no posting.</summary>
    public Guid? OrderId { get; set; }
    public Order? Order { get; set; }

    public Guid? OrderMarketplaceItemId { get; set; }
    public OrderMarketplaceItem? OrderMarketplaceItem { get; set; }

    /// <summary>Copied from the matched line, like <see cref="MarketplaceReturn.CatalogItemId"/>.</summary>
    public Guid? CatalogItemId { get; set; }
    public CatalogItem? CatalogItem { get; set; }

    public DateTime SyncedAt { get; set; }
}
