using System.ComponentModel.DataAnnotations;
using ProjectWarehouse.Server.Domain;

namespace ProjectWarehouse.Server.Models.Integrations;

public class AutoMapCardsRequest
{
    [MinLength(1)]
    [MaxLength(50)]
    public IReadOnlyList<Guid> AccountIds { get; init; } = [];

    /// <summary>Re-match cards mapped by a rule, by article or by barcode.</summary>
    public bool OverwriteAuto { get; init; }

    /// <summary>Re-match cards a human mapped by hand.</summary>
    public bool OverwriteManual { get; init; }

    /// <summary>A re-matched card that finds no unambiguous match loses its mapping instead of keeping it.</summary>
    public bool ClearUnmatched { get; init; }

    /// <summary>Compute and return the changes without saving them.</summary>
    public bool DryRun { get; init; }
}

public class AutoMapCardsResponse
{
    public int Mapped { get; init; }
    public int Cleared { get; init; }

    /// <summary>Active unmapped cards across the requested accounts once the changes are applied.</summary>
    public int Remaining { get; init; }

    /// <summary>Only cards whose mapping changes.</summary>
    public IReadOnlyList<AutoMapCardChangeDto> Items { get; init; } = [];
}

public class AutoMapCardChangeDto
{
    public Guid CardId { get; init; }
    public Guid AccountId { get; init; }
    public string AccountName { get; init; } = null!;
    public string OfferId { get; init; } = null!;
    public string CardName { get; init; } = null!;

    public Guid? OldCatalogItemId { get; init; }
    public string? OldCatalogItemFullName { get; init; }
    public string? OldCatalogItemArticle { get; init; }
    public MarketplaceMappingSource? OldMappingSource { get; init; }

    public Guid? NewCatalogItemId { get; init; }
    public string? NewCatalogItemFullName { get; init; }
    public string? NewCatalogItemArticle { get; init; }
    public MarketplaceMappingSource? NewMappingSource { get; init; }
}
