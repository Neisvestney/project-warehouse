namespace ProjectWarehouse.Server.Integrations.Abstractions;

/// <summary>One supply as the marketplace states it; see <see cref="Domain.MarketplaceSupply"/>.</summary>
public record ExternalSupply(
    string ExternalId,
    string? Name,
    DateTime CreatedAt,
    DateTime? ClosedAt,
    bool IsDone);

/// <summary>The postings a supply holds right now.</summary>
public record ExternalSupplyPostings(string SupplyExternalId, IReadOnlyList<string> PostingNumbers);
