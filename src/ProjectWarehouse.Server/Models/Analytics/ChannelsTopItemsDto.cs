using ProjectWarehouse.Server.Domain;

namespace ProjectWarehouse.Server.Models.Analytics;

public class ChannelsTopItemsDto
{
    public DateOnly From { get; init; }
    public DateOnly To { get; init; }
    public string TimeZoneId { get; init; } = null!;
    public AnalyticsTopItemsBy By { get; init; }
    public AnalyticsMoneyMode MoneyMode { get; init; }

    /// <summary>Currency of <see cref="TopItemDto.Money"/>; null when the channels sold nothing with a price.</summary>
    public string? CurrencyCode { get; init; }

    /// <summary>Currencies met on the sale lines, the most frequent first.</summary>
    public List<string> Currencies { get; init; } = [];

    /// <summary>Items with a positive ranking value, before <c>take</c> cuts the list.</summary>
    public int TotalItems { get; init; }

    /// <summary>Marketplace sale lines with no catalog item: counted by the channel, absent from the ranking.</summary>
    public int UnlinkedLines { get; init; }

    public List<TopItemDto> Items { get; init; } = [];
}

public class TopItemDto
{
    public Guid CatalogItemId { get; init; }
    public string Name { get; init; } = null!;
    public CatalogItemType Type { get; init; }
    public int Units { get; init; }

    /// <summary>In <see cref="ChannelsTopItemsDto.CurrencyCode"/>; null when the item has none in it.</summary>
    public decimal? Money { get; init; }

    /// <summary>Share of the ranking value among all ranked items.</summary>
    public double Share { get; init; }
}
