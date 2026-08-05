using ProjectWarehouse.Server.Domain;
using ProjectWarehouse.Server.Infrastructure;
using ProjectWarehouse.Server.Integrations.Abstractions;

namespace ProjectWarehouse.Server.Models.Integrations;

/// <summary>
/// Has no API key field at all — there is physically nothing to leak. The UI gets a mask instead.
/// </summary>
public class MarketplaceAccountDto : IHasIdentity
{
    public Guid Id { get; init; }
    public MarketplaceType Type { get; init; }
    /// <summary>Reported by the marketplace, not editable. A placeholder until the first sync.</summary>
    public string Name { get; init; } = null!;

    public bool IsActive { get; init; }
    public string? ExternalClientId { get; init; }

    public string? CompanyLegalName { get; init; }
    public string? Inn { get; init; }
    public string? Ogrn { get; init; }
    public string? OwnershipForm { get; init; }

    /// <summary>Key tail only — the client renders the mask.</summary>
    public string ApiKeyLast4 { get; init; } = null!;
    public DateTime? ApiKeyUpdatedAt { get; init; }

    /// <summary>The stored key can no longer be decrypted — the key ring was lost. Probed, not stored.</summary>
    public bool CredentialsUnreadable { get; set; }

    public MarketplaceCapabilities Capabilities { get; set; }

    public int SyncIntervalMinutes { get; init; }
    public DateTime? LastSyncAt { get; init; }
    public MarketplaceSyncStatus? LastSyncStatus { get; init; }
    public AppFieldError? LastSyncError { get; init; }

    public DateTime CreatedAt { get; init; }
    public Guid? CreatedById { get; init; }
    public string? CreatedByName { get; init; }

    public int WarehouseCount { get; init; }
    public int UnmappedWarehouseCount { get; init; }
    public int CardCount { get; init; }
    public int UnmappedCardCount { get; init; }
}
