using ProjectWarehouse.Server.Domain;

namespace ProjectWarehouse.Server.Integrations.Abstractions;

/// <summary>One marketplace accrual, already cut into money movements; see <see cref="MarketplaceAccrual"/>.</summary>
public record ExternalAccrual(
    string ExternalId,
    DateOnly Date,
    MarketplaceAccrualScope Scope,
    string? UnitNumber,
    IReadOnlyList<ExternalAccrualLine> Lines,
    MarketplaceAccrualSource Source = MarketplaceAccrualSource.AccrualJournal);

public record ExternalAccrualLine(
    MarketplaceAccrualCategory Category,
    string? RawTypeId,
    string? Sku,
    decimal Amount,
    string? CurrencyCode);
