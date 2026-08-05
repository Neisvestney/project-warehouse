namespace ProjectWarehouse.Server.Models.Integrations;

/// <summary>Feeds the sidebar badge — a count query, never a card listing.</summary>
public class UnmappedCardsCountDto
{
    public int Count { get; init; }
}
