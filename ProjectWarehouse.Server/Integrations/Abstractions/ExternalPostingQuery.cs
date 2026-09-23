using ProjectWarehouse.Server.Domain;

namespace ProjectWarehouse.Server.Integrations.Abstractions;

/// <summary>
/// A period of postings to list. The provider slices the period into whatever windows its API tolerates
/// and expands <see cref="Statuses"/> into the marketplace's own vocabulary; null means every status.
/// </summary>
public record ExternalPostingQuery(
    ExternalPostingScheme Scheme,
    DateTime Since,
    DateTime To,
    IReadOnlyList<MarketplaceOrderStatus>? Statuses = null);
