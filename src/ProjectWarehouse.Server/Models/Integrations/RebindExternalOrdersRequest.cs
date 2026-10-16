using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using ProjectWarehouse.Server.Infrastructure;

namespace ProjectWarehouse.Server.Models.Integrations;

public class RebindExternalOrdersRequest
{
    [MinLength(1)]
    [MaxLength(50)]
    public IReadOnlyList<Guid> AccountIds { get; init; } = [];

    /// <summary>Orders whose effective date is at or after this instant are rebound.</summary>
    [JsonRequired]
    public DateTime Since { get; init; }

    /// <summary>Compute and return the changes without saving them.</summary>
    public bool DryRun { get; init; }
}

public class RebindExternalOrdersResponse
{
    public int Orders { get; init; }
    public int Lines { get; init; }

    /// <summary>One row per card and catalog item its lines move away from, ordered by account and offer id.</summary>
    public IReadOnlyList<RebindExternalOrdersItemDto> Items { get; init; } = [];
}

public class RebindExternalOrdersItemDto
{
    public Guid CardId { get; init; }
    public Guid AccountId { get; init; }
    public string AccountName { get; init; } = null!;
    public string OfferId { get; init; } = null!;
    public string CardName { get; init; } = null!;

    /// <summary>Null for lines imported while the card had no mapping.</summary>
    public Guid? OldCatalogItemId { get; init; }
    public string? OldCatalogItemFullName { get; init; }
    public string? OldCatalogItemArticle { get; init; }

    public Guid NewCatalogItemId { get; init; }
    public string NewCatalogItemFullName { get; init; } = null!;
    public string? NewCatalogItemArticle { get; init; }

    public int Orders { get; init; }
    public int Lines { get; init; }
}

/// <summary>
/// Changelog snapshot of a rebind on one account: per card, the catalog items its external order lines
/// pointed at. Lines themselves are not listed — a period of history runs to thousands of them.
/// </summary>
public class ExternalOrderBindingsSnapshot : IHasIdentity
{
    public Guid Id { get; init; }
    public IReadOnlyList<ExternalOrderBindingSnapshotItem> Cards { get; init; } = [];
}

public class ExternalOrderBindingSnapshotItem : IHasIdentity
{
    public Guid Id { get; init; }
    public string OfferId { get; init; } = null!;
    public IReadOnlyList<Guid?> CatalogItemIds { get; init; } = [];
}
