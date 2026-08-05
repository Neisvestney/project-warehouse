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
}

public class OzonOptions
{
    public string BaseUrl { get; set; } = "https://api-seller.ozon.ru";

    public int TimeoutSeconds { get; set; } = 60;

    /// <summary>Pause between pages, on top of the resilience handler's 429 handling.</summary>
    public int PageDelayMs { get; set; } = 200;
}
