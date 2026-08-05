using System.ComponentModel.DataAnnotations;

namespace ProjectWarehouse.Server.Models.Integrations;

public class UpdateMarketplaceAccountRequest
{
    public string? ClientId { get; init; }

    /// <summary>Write-only. Empty or absent means "keep the current key".</summary>
    public string? ApiKey { get; init; }

    [Range(1, 10080)]
    public int SyncIntervalMinutes { get; init; }

    public bool IsActive { get; init; }
}
