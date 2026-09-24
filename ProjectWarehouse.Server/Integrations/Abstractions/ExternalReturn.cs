using ProjectWarehouse.Server.Domain;

namespace ProjectWarehouse.Server.Integrations.Abstractions;

/// <summary>One returned unit as the marketplace states it; see <see cref="MarketplaceReturn"/>.</summary>
public record ExternalReturn(
    string ExternalId,
    string PostingNumber,
    string? Sku,
    string OfferId,
    MarketplaceReturnScheme Scheme,
    string? RawScheme,
    MarketplaceReturnKind Kind,
    string? RawKind,
    int Quantity,
    decimal? Price,
    string? CurrencyCode,
    string? Reason,
    string? RawStatus,
    string? StatusName,
    bool IsCancelled,
    DateTime? StatusChangedAt,
    DateTime? ReturnedAt,
    DateTime? FinalAt,
    MarketplaceReturnCompensationStatus? CompensationStatus,
    DateTime? CompensationStatusChangedAt,
    string? SourceExternalId,
    string? ExemplarId);
