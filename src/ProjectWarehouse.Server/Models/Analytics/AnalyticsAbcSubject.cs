namespace ProjectWarehouse.Server.Models.Analytics;

/// <summary>What one row of the ABC analysis is.</summary>
public enum AnalyticsAbcSubject
{
    /// <summary>The catalog item a sale line was imported with; Direct orders take part.</summary>
    CatalogItem = 0,

    /// <summary>One marketplace card of one account; shops only.</summary>
    Card = 1,

    /// <summary>The cards of every selected account sharing an offer id, compared trimmed and case-blind; shops only.</summary>
    Article = 2,
}
