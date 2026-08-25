namespace ProjectWarehouse.Server.Infrastructure;

/// <summary>Named action constants used in ChangeLog entries produced by the stock forecast.</summary>
public static class ForecastActions
{
    /// <summary>Per-item warning threshold on one warehouse written or cleared.</summary>
    public const string OverrideSet     = "forecast.override_set";
    public const string OverrideCleared = "forecast.override_cleared";
}
