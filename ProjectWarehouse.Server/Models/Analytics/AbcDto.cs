using ProjectWarehouse.Server.Domain;

namespace ProjectWarehouse.Server.Models.Analytics;

public class AbcDto
{
    public DateOnly From { get; init; }
    public DateOnly To { get; init; }
    public string TimeZoneId { get; init; } = null!;
    public AnalyticsAbcBasis Basis { get; init; }
    public AnalyticsAbcSubject Subject { get; init; }

    /// <summary>Currency of a money basis; null for units and when the shops sold nothing with a price.</summary>
    public string? CurrencyCode { get; init; }

    /// <summary>Currencies met on the sale lines, the most frequent first.</summary>
    public List<string> Currencies { get; init; } = [];

    /// <summary>Accrued lines among the sale lines in the currency; filled for the payout basis only.</summary>
    public double? PayoutCoverage { get; init; }

    /// <summary>
    /// Marketplace sale lines with no row of the subject — no catalog item, or no card: counted by the channel,
    /// absent from the analysis.
    /// </summary>
    public int UnlinkedLines { get; init; }

    /// <summary>Σ of every analysed item's value.</summary>
    public decimal TotalValue { get; init; }

    public AbcAppliedSettingsDto Settings { get; init; } = null!;

    /// <summary>Full XYZ intervals the period holds that are already over; below <see cref="AbcAppliedSettingsDto.XyzMinIntervals"/> no item gets a class.</summary>
    public int XyzIntervals { get; init; }

    /// <summary>Whether each item's XYZ series starts at its first sale ever rather than at the period start.</summary>
    public bool XyzFromFirstSale { get; init; }

    /// <summary>A, B and C, in that order, each present even when empty.</summary>
    public List<AbcClassSummaryDto> Classes { get; init; } = [];

    /// <summary>Items per ABC × XYZ pair; the pairs with no XYZ class carry a null <c>xyzClass</c>.</summary>
    public List<AbcMatrixCellDto> Matrix { get; init; } = [];

    /// <summary>Every analysed item in rank order, for the Pareto chart.</summary>
    public List<AbcParetoPointDto> Pareto { get; init; } = [];

    /// <summary>The table page, narrowed by the class filters and the search; ranks stay those of the whole analysis.</summary>
    public Paginated<AbcItemDto> Items { get; init; } = null!;
}

/// <summary>The parameters the classes were computed with.</summary>
public class AbcAppliedSettingsDto
{
    public decimal AbcBoundaryA { get; init; }
    public decimal AbcBoundaryB { get; init; }
    public decimal XyzBoundaryX { get; init; }
    public decimal XyzBoundaryY { get; init; }
    public AnalyticsXyzStep XyzStep { get; init; }
    public int XyzMinIntervals { get; init; }
}

public class AbcClassSummaryDto
{
    public AbcClass Class { get; init; }
    public int Items { get; init; }

    /// <summary>Share of the analysed items, not of the whole catalog.</summary>
    public double ItemsShare { get; init; }

    public decimal Value { get; init; }
    public double ValueShare { get; init; }
}

public class AbcMatrixCellDto
{
    public AbcClass AbcClass { get; init; }
    public XyzClass? XyzClass { get; init; }
    public int Items { get; init; }
}

public class AbcParetoPointDto
{
    public decimal Value { get; init; }
    public AbcClass Class { get; init; }
}

/// <summary>A row of the analysis: a catalog item, a card or an article, by <see cref="AbcDto.Subject"/>.</summary>
public class AbcSubjectDto
{
    /// <summary>The catalog item id, the card id or the normalized article; unique within one analysis.</summary>
    public string Key { get; init; } = null!;

    /// <summary>The item's full name, the card's name, or the name of the article's best-selling card.</summary>
    public string Name { get; init; } = null!;

    /// <summary>Set for a catalog item only, as is <see cref="Type"/>.</summary>
    public Guid? CatalogItemId { get; init; }

    public CatalogItemType? Type { get; init; }

    /// <summary>Set for a card only.</summary>
    public Guid? MarketplaceCardId { get; init; }

    /// <summary>The card's offer id, or the article as its best-selling card spells it.</summary>
    public string? OfferId { get; init; }

    public string? ImageUrl { get; init; }

    /// <summary>The card's account, or every account the article sold on; empty for a catalog item.</summary>
    public List<AbcSubjectAccountDto> Accounts { get; init; } = [];
}

public class AbcSubjectAccountDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = null!;
    public MarketplaceType Type { get; init; }
}

public class AbcItemDto
{
    /// <summary>Place in the whole analysis, 1-based.</summary>
    public int Rank { get; init; }

    public AbcSubjectDto Subject { get; init; } = null!;

    public decimal Value { get; init; }
    public double Share { get; init; }

    /// <summary>Cumulative share up to and including this item.</summary>
    public double CumulativeShare { get; init; }

    public AbcClass AbcClass { get; init; }

    /// <summary>
    /// Null when the item has fewer full intervals than <see cref="AbcAppliedSettingsDto.XyzMinIntervals"/> — the
    /// period is too short or, with <see cref="AbcDto.XyzFromFirstSale"/>, the item too new — or sold only in the
    /// partial ones.
    /// </summary>
    public XyzClass? XyzClass { get; init; }

    /// <summary>Full intervals the item's XYZ series spans; null when the period holds too few for anyone.</summary>
    public int? XyzIntervals { get; init; }

    /// <summary>Coefficient of variation as a fraction; null under the same conditions as <see cref="XyzClass"/>.</summary>
    public double? Cv { get; init; }
}
