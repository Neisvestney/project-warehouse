namespace ProjectWarehouse.Server.Integrations.Abstractions;

/// <summary>
/// Seller identity as the marketplace knows it. Every field is optional — a marketplace may expose
/// only part of it, and a self-employed seller has no OGRN at all.
/// </summary>
public record ExternalSellerInfo(
    string? Name,
    string? LegalName,
    string? Inn,
    string? Ogrn,
    string? OwnershipForm);
