namespace ProjectWarehouse.Server.Integrations.Abstractions;

public record ExternalCard(
    string ExternalId,
    string? Sku,
    string OfferId,
    string Name,
    IReadOnlyList<string> Barcodes,
    string? ImageUrl,
    decimal? Price,
    string? Currency,
    bool IsArchived);
