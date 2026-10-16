using System.Text.RegularExpressions;
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

    /// <summary>GTIN-14 as registered in Честный знак; may differ from <see cref="Barcode"/>.</summary>
    public string? Gtin { get; set; }
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

    // PostgreSQL ARE syntax, kept ASCII-only so the SQL log stays readable; any non-space whitespace or invisible char
    internal const string IrregularWhitespacePattern =
        @"[\t\n\r\v\f\u0085\u00A0\u00AD\u1680\u2000-\u200F\u2028\u2029\u202F\u205F\u2060\u3000\uFEFF]";

    internal const string TextWhitespaceIssuePattern = @"^[ ]|[ ]$|[ ]{2}|" + IrregularWhitespacePattern;

    internal const string CodeWhitespaceIssuePattern = @"[ ]|" + IrregularWhitespacePattern;

    [Projectable]
    public bool HasOwnWhitespaceIssues =>
        Regex.IsMatch(Name, TextWhitespaceIssuePattern) ||
        Regex.IsMatch(Article, TextWhitespaceIssuePattern) ||
        (LabelText != null && Regex.IsMatch(LabelText, TextWhitespaceIssuePattern)) ||
        (Barcode != null && Regex.IsMatch(Barcode, CodeWhitespaceIssuePattern)) ||
        (Gtin != null && Regex.IsMatch(Gtin, CodeWhitespaceIssuePattern));

    /// <summary>A product group counts its children too, since the catalog list shows only the group.</summary>
    [Projectable]
    public bool HasWhitespaceIssues => HasOwnWhitespaceIssues || GroupChildren.Any(c => c.HasOwnWhitespaceIssues);

    [Projectable]
    public string SearchString => (Name ?? "") + " " + (Article ?? "") + " " + (Barcode ?? "") + " " + (Gtin ?? "") + " " + (Description ?? "") +
                                  (Group != null ? (Group.Name + " " + Group.Article + " " + Group.Barcode ?? "") : "");
}