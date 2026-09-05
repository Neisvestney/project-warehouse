using EntityFrameworkCore.Projectables;
using ProjectWarehouse.Server.Infrastructure;

namespace ProjectWarehouse.Server.Domain;

public class CatalogItem : IHasIdentity
{
    public Guid Id { get; set; }
    public CatalogItemType Type { get; set; }
    public string Name { get; set; } = null!;
    public string Article { get; set; } = null!;
    public string? Barcode { get; set; }
    public string? Description { get; set; }
    public string? Notes { get; set; }
    public string? LabelText { get; set; }
    public bool IsArchived { get; set; }

    public Guid? GroupId { get; set; }
    public CatalogItem? Group { get; set; }
    public ICollection<CatalogItem> GroupChildren { get; set; } = [];

    public ICollection<CatalogItemVariationMember> VariationMemberships { get; set; } = [];
    public ICollection<CatalogItemVariationMember> VariationMembers { get; set; } = [];

    public ICollection<BundleComponent> BundleComponents { get; set; } = [];

    public ICollection<CatalogItemTag> Tags { get; set; } = [];

    public Guid? MainImageFileId { get; set; }
    public DataFile? MainImageFile { get; set; }

    // List<>, not ICollection<>: the identity-based IListUpdater overload takes List<T>
    public List<CatalogItemImage> Images { get; set; } = [];

    public ICollection<MarketplaceCard> MarketplaceCards { get; set; } = [];

    [Projectable] public string FullName => Group != null ? Group.Name + " " + Name : Name;

    [Projectable] public string? EffectiveDescription => Description ?? (Group != null ? Group.Description : null);

    [Projectable] public string? EffectiveNotes => Notes ?? (Group != null ? Group.Notes : null);

    [Projectable] public string EffectiveLabelText => LabelText ?? Article;

    [Projectable]
    public string SearchString => (Name ?? "") + " " + (Article ?? "") + " " + (Barcode ?? "") + " " + (Description ?? "") +
                                  (Group != null ? (Group.Name + " " + Group.Article + " " + Group.Barcode ?? "") : "");
}