using ProjectWarehouse.Server.Domain;

namespace ProjectWarehouse.Server.Integrations.Abstractions;

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
    IReadOnlyList<ExternalPostingItem> Items);
