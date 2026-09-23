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

    /// <summary>Distance from the right page edge, in points.</summary>
    public double MarginX { get; set; } = 6;

    /// <summary>Distance from the top page edge, in points.</summary>
    public double MarginY { get; set; } = 6;

    /// <summary>Age after which a cached label of a posting past awaiting_deliver is released.</summary>
    public int CacheTtlDays { get; set; } = 7;

    public string GcCron { get; set; } = "0 15 3 * * ?";
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

    /// <summary>
    /// Half-widths of the mandatory <c>since</c>/<c>to</c> window on /v4/posting/fbs/list, in days.
    /// The status catch-up asks about postings by order number, so the window only has to be wide
    /// enough not to cut one off; Ozon caps the period at a year, which is what the two together
    /// stay under. A posting outside the window comes back as absent, i.e. as forgotten.
    /// </summary>
    public int PostingWindowPastDays { get; set; } = 330;

    public int PostingWindowFutureDays { get; set; } = 30;

    /// <summary>
    /// Width of one <c>since</c>/<c>to</c> slice when listing postings over an explicit period. Ozon
    /// answers <c>PERIOD_IS_TOO_LONG</c> past a year, so a longer request is cut into slices of this many
    /// days. Same ceiling as <see cref="PostingWindowPastDays"/>, and kept under a year for the same reason.
    /// </summary>
    public int PostingPeriodWindowDays { get; set; } = 330;

    /// <summary>
    /// How far back the "when did this account start selling" probe walks, in
    /// <see cref="PostingPeriodWindowDays"/> slices. It stops early at the first empty slice; this is only
    /// the ceiling on how many calls one probe may cost.
    /// </summary>
    public int EarliestPostingProbeWindows { get; set; } = 5;

    /// <summary>
    /// How far back the background FBO import looks on an account it has never imported before. Afterwards
    /// the period starts from <see cref="Domain.MarketplaceAccount.FboPostingsSyncedAt"/> instead.
    /// </summary>
    public int FboImportWindowPastDays { get; set; } = 14;

    /// <summary>
    /// How far before the last import the background FBO import starts anyway. A posting can surface in
    /// Ozon's list later than it was created — replicas lag and its clock is not ours — and a period
    /// beginning exactly at the last import would miss it for good, since no later period covers it
    /// either. The cost of the overlap is re-reading that much of the period every run.
    /// </summary>
    public int FboImportOverlapHours { get; set; } = 6;
}
