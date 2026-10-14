namespace ProjectWarehouse.Server.Models.Orders;

/// <summary>Marketplace side of an order on its own page — the list has no use for the parcels.</summary>
public class MarketplaceOrderDetailsDto : MarketplaceOrderDto
{
    /// <summary>By place number; every marketplace order has at least one.</summary>
    public IReadOnlyList<MarketplaceParcelDto> Parcels { get; init; } = [];
}

/// <summary>A place of the posting that leaves with a label of its own.</summary>
public class MarketplaceParcelDto
{
    public Guid Id { get; init; }
    public int Index { get; init; }
    public string? ExternalId { get; init; }
    public string? Barcode { get; init; }
    public Guid? OrderBoxId { get; init; }

    /// <summary>Set once the label has been printed; downloadable through /api/files/{id}/content.</summary>
    public Guid? LabelFileId { get; init; }
    public DateTime? LabelFetchedAt { get; init; }
    public AppFieldError? LabelError { get; init; }
}
