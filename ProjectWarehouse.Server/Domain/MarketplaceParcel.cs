using System.ComponentModel.DataAnnotations.Schema;
using ProjectWarehouse.Server.Infrastructure.Marketplaces;
using ProjectWarehouse.Server.Models;

namespace ProjectWarehouse.Server.Domain;

/// <summary>
/// A physical place of a posting that leaves with a marketplace label of its own. Not a WMS box:
/// <see cref="OrderBox"/> says what the packer put together, a parcel says what the marketplace accepts as one unit.
/// </summary>
public class MarketplaceParcel
{
    public Guid Id { get; set; }

    public Guid MarketplaceOrderId { get; set; }
    public MarketplaceOrder MarketplaceOrder { get; set; } = null!;

    /// <summary>1-based place number within the posting; pages of one order print in this order.</summary>
    public int Index { get; set; }

    /// <summary>The marketplace's id of the place, where it has one.</summary>
    public string? ExternalId { get; set; }

    /// <summary>Barcode of the place as printed on its label, where the marketplace states one.</summary>
    public string? Barcode { get; set; }

    /// <summary>The WMS box this place coincides with; gone when the packer deletes the box.</summary>
    public Guid? OrderBoxId { get; set; }
    public OrderBox? OrderBox { get; set; }

    /// <summary>The marketplace label as fetched, with nothing stamped on it — articles go on at print time.</summary>
    public Guid? LabelFileId { get; set; }
    public DataFile? LabelFile { get; set; }
    public DateTime? LabelFetchedAt { get; set; }

    /// <summary>
    /// Format of <see cref="LabelFile"/>, which picks the corner the articles are printed into. Null when the
    /// format could not be identified on fetch — it is then identified again on every print.
    /// </summary>
    public LabelKind? LabelKind { get; set; }

    [Column(TypeName = "jsonb")] public AppFieldError? LabelError { get; set; }

    /// <summary>What the place holds. Empty — the place is the whole posting.</summary>
    public List<MarketplaceParcelItem> Items { get; set; } = [];
}

/// <summary>Either whole units (<see cref="Quantity"/>) or part <see cref="PartIndex"/> of <see cref="PartCount"/> of one unit.</summary>
public class MarketplaceParcelItem
{
    public Guid MarketplaceParcelId { get; set; }
    public MarketplaceParcel MarketplaceParcel { get; set; } = null!;

    public Guid OrderMarketplaceItemId { get; set; }
    public OrderMarketplaceItem OrderMarketplaceItem { get; set; } = null!;

    public int? Quantity { get; set; }
    public int? PartIndex { get; set; }
    public int? PartCount { get; set; }
}
