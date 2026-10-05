using ProjectWarehouse.Server.Domain;
using ProjectWarehouse.Server.Infrastructure;

namespace ProjectWarehouse.Server.Models.Organizations;

public class OrganizationDto : IHasIdentity
{
    public Guid Id { get; init; }
    public string Name { get; init; } = null!;
    public string Inn { get; init; } = null!;
    public OrganizationKind Kind { get; init; }
    public string? LegalName { get; init; }
    public string? Kpp { get; init; }
    public string? Ogrn { get; init; }
    public string? OwnershipForm { get; init; }

    public DateTime CreatedAt { get; init; }
    public Guid? CreatedById { get; init; }
    public string? CreatedByName { get; init; }

    public List<OrganizationAccountDto> Accounts { get; init; } = [];
}

public class OrganizationAccountDto : IHasIdentity
{
    public Guid Id { get; init; }
    public MarketplaceType Type { get; init; }
    public string Name { get; init; } = null!;
    public bool IsActive { get; init; }

    /// <summary>The account's own INN as the marketplace reports it — may differ from the organization's.</summary>
    public string? Inn { get; init; }

    public bool IsOrganizationLinkedManually { get; init; }
}

public class OrganizationSummaryDto : IHasIdentity
{
    public Guid Id { get; init; }
    public string Name { get; init; } = null!;
    public string Inn { get; init; } = null!;
    public OrganizationKind Kind { get; init; }
    public string? LegalName { get; init; }
    public string? OwnershipForm { get; init; }
    public int AccountCount { get; init; }
}

public class OrganizationShortSummaryDto : IHasIdentity
{
    public Guid Id { get; init; }
    public string Name { get; init; } = null!;
    public string Inn { get; init; } = null!;
}

public enum OrganizationSortBy
{
    Name = 0,
    Inn = 1,
    CreatedAt = 2,
}
