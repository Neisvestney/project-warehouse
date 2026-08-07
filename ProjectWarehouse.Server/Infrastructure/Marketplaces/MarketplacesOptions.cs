namespace ProjectWarehouse.Server.Infrastructure.Marketplaces;

public class MarketplacesOptions
{
    public const string SectionName = "Marketplaces";

    /// <summary>Data Protection key ring location. Losing it makes stored API keys undecryptable.</summary>
    public string KeyRingPath { get; set; } = "/keys";

    /// <summary>Quartz cron of the job that picks accounts due for a background sync.</summary>
    public string SyncScanCron { get; set; } = "0 * * * * ?";

    public int DefaultSyncIntervalMinutes { get; set; } = 30;

    public OzonOptions Ozon { get; set; } = new();

    public LabelsOptions Labels { get; set; } = new();
}

public class LabelsOptions
{
    /// <summary>Articles printed along the label edge before collapsing the rest into "+N".</summary>
    public int MaxArticlesOnLabel { get; set; } = 3;

    public string FontResourceName { get; set; } = "ProjectWarehouse.Server.Resources.Fonts.LabelFont.ttf";

    public double FontSize { get; set; } = 8;

    /// <summary>Distance from the page edge, in points.</summary>
    public double Margin { get; set; } = 6;
}

public class OzonOptions
{
    public string BaseUrl { get; set; } = "https://api-seller.ozon.ru";

    public int TimeoutSeconds { get; set; } = 60;

    /// <summary>Pause between pages, on top of the resilience handler's 429 handling.</summary>
    public int PageDelayMs { get; set; } = 200;

    /// <summary>Configurable, but 20 is Ozon's own ceiling — anything higher comes back as its error.</summary>
    public int LabelBatchSize { get; set; } = 20;

    /// <summary>
    /// Half-widths of the mandatory cutoff window on /v4/posting/fbs/unfulfilled/list, in days.
    /// Ozon rejects the call without one ("either filter.cutoff or filter.delivering_date must be
    /// specified") even though its spec marks no filter field as required. Cutoff is the assembly
    /// deadline, which for awaiting_deliver postings sits near today, so the window is deliberately
    /// far wider than needed: broadening it costs nothing — the status filter is what narrows the
    /// result — while a narrow one would silently drop a posting at the edge.
    /// </summary>
    public int CutoffWindowPastDays { get; set; } = 90;

    public int CutoffWindowFutureDays { get; set; } = 180;
}
