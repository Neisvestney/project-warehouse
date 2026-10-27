using ProjectWarehouse.Server.Domain;

namespace ProjectWarehouse.Server.Integrations.Abstractions;

/// <summary>
/// One posting as the marketplace states it. Fields an FBO posting has no equivalent for
/// (<see cref="TrackingNumber"/>, <see cref="DeliveryMethodName"/>, <see cref="ShipmentDate"/>,
/// <see cref="WarehouseExternalId"/>, <see cref="LabelBarcode"/>, <see cref="Dates"/>) arrive null — see
/// <see cref="Scheme"/>.
/// <see cref="Parcels"/> is null when the marketplace states no layout: the posting is then one parcel.
/// </summary>
public record ExternalPosting(
    string PostingNumber,
    string? ExternalOrderNumber,
    MarketplaceOrderStatus Status,
    string? RawStatus,
    string? RawSubstatus,
    string? WarehouseExternalId,
    string? DeliveryMethodName,
    DateTime? ShipmentDate,
    DateTime? InProcessAt,
    string? TrackingNumber,
    int MultiBoxQty,
    ExternalCancellation? Cancellation,
    IReadOnlyList<ExternalPostingItem> Items,
    ExternalPostingScheme Scheme = ExternalPostingScheme.Fbs,
    DateTime? CreatedAt = null,
    string? LabelBarcode = null,
    IReadOnlyList<ExternalParcel>? Parcels = null,
    ExternalPostingDates? Dates = null);
