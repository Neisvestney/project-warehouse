using EntityFrameworkCore.Projectables;
using ProjectWarehouse.Server.Infrastructure;

namespace ProjectWarehouse.Server.Domain;

/// <summary>
/// The seller company behind one or more marketplace accounts, identified by its INN. Covers both legal
/// entities and sole proprietors. Its requisites are its own copy: sync fills them only when it creates the
/// organization and never overwrites them afterwards.
/// </summary>
public class Organization : IHasIdentity
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;

    /// <summary>Unique. 10 digits for a legal entity, 12 for a sole proprietor.</summary>
    public string Inn { get; set; } = null!;

    public string? LegalName { get; set; }
    public string? Kpp { get; set; }
    public string? Ogrn { get; set; }
    public string? OwnershipForm { get; set; }

    public DateTime CreatedAt { get; set; }
    public Guid? CreatedById { get; set; }
    public ApplicationUser? CreatedBy { get; set; }

    public ICollection<MarketplaceAccount> Accounts { get; set; } = [];

    /// <summary>Derived, not stored: a 12-digit INN belongs to a sole proprietor, a 10-digit one to a legal entity.</summary>
    [Projectable]
    public OrganizationKind Kind => Inn.Length == 12 ? OrganizationKind.SoleProprietor : OrganizationKind.LegalEntity;

    [Projectable]
    public string SearchString => Name + " " + (LegalName ?? "") + " " + Inn;
}
