using ProjectWarehouse.Server.Integrations.Abstractions;

namespace ProjectWarehouse.Server.Integrations.Ozon;

/// <summary>
/// Domain-shaped wrapper over the generated client: owns pagination and response mapping so the
/// generated types never escape this folder.
/// </summary>
public interface IOzonClient
{
    Task<IReadOnlyList<ExternalWarehouse>> GetWarehousesAsync(CancellationToken ct);

    IAsyncEnumerable<IReadOnlyList<ExternalCard>> GetCardsAsync(CancellationToken ct);

    Task<ExternalSellerInfo> GetSellerInfoAsync(CancellationToken ct);

    /// <summary>Cheapest call that proves the credentials work.</summary>
    Task PingAsync(CancellationToken ct);
}
