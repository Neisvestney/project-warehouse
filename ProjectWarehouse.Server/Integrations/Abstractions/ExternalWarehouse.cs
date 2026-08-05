using ProjectWarehouse.Server.Domain;

namespace ProjectWarehouse.Server.Integrations.Abstractions;

public record ExternalWarehouse(
    string ExternalId,
    string Name,
    MarketplaceWarehouseKind Kind,
    string? Status,
    string? Address);
