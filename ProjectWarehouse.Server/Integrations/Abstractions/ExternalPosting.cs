using ProjectWarehouse.Server.Domain;

namespace ProjectWarehouse.Server.Integrations.Abstractions;

/// <summary>
/// One posting as the marketplace states it. Fields an FBO posting has no equivalent for
/// (<see cref="TrackingNumber"/>, <see cref="DeliveryMethodName"/>, <see cref="ShipmentDate"/>,
/// <see cref="WarehouseExternalId"/>) arrive null — see <see cref="Scheme"/>.
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
    DateTime? CreatedAt = null);
