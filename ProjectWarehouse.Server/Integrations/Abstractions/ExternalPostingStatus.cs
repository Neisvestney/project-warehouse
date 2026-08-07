using ProjectWarehouse.Server.Domain;

namespace ProjectWarehouse.Server.Integrations.Abstractions;

public record ExternalPostingStatus(
    string PostingNumber,
    MarketplaceOrderStatus Status,
    string? RawStatus,
    string? RawSubstatus,
    string? TrackingNumber);
