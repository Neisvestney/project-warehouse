using ProjectWarehouse.Server.Domain;

namespace ProjectWarehouse.Server.Integrations.Abstractions;

/// <summary>
/// Why a posting was cancelled. Null on a posting that is not cancelled — marketplaces tend to answer
/// with an empty object rather than omitting it, and an empty one carries nothing.
/// </summary>
public record ExternalCancellation(
    bool? CancelledAfterShip,
    MarketplaceCancellationType Type,
    string? RawType,
    string? Reason);
