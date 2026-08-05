using ProjectWarehouse.Server.Domain;

namespace ProjectWarehouse.Server.Integrations.Abstractions;

/// <summary>
/// <c>Status</c> is normalised availability — each provider owns the mapping from its own vocabulary;
/// <c>RawStatus</c> carries the marketplace wording through for diagnostics only.
/// </summary>
public record ExternalWarehouse(
    string ExternalId,
    string Name,
    MarketplaceWarehouseKind Kind,
    MarketplaceWarehouseStatus Status,
    string? RawStatus,
    string? Address);
