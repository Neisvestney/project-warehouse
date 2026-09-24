using ProjectWarehouse.Server.Domain;

namespace ProjectWarehouse.Server.Integrations.Abstractions;

public enum ExternalReturnDateKind
{
    /// <summary>When the return last changed status — catches both new returns and their movement.</summary>
    StatusChanged = 0,

    /// <summary>When the buyer handed the item back or refused it.</summary>
    Returned = 1,
}

/// <summary>
/// Either a period over one kind of date or a compensation status, never both: Ozon accepts a single date
/// filter per request, and compensation changes do not move any date it filters by.
/// </summary>
public sealed record ExternalReturnQuery
{
    private ExternalReturnQuery()
    {
    }

    public ExternalReturnDateKind DateKind { get; private init; }
    public DateTime? Since { get; private init; }
    public DateTime? To { get; private init; }

    public MarketplaceReturnCompensationStatus? CompensationStatus { get; private init; }

    public static ExternalReturnQuery ByPeriod(ExternalReturnDateKind kind, DateTime since, DateTime to) =>
        new() { DateKind = kind, Since = since, To = to };

    public static ExternalReturnQuery ByCompensation(MarketplaceReturnCompensationStatus status) =>
        new() { CompensationStatus = status };
}
