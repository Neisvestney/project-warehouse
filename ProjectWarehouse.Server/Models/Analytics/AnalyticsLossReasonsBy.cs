namespace ProjectWarehouse.Server.Models.Analytics;

public enum AnalyticsLossReasonsBy
{
    Units = 0,

    /// <summary>Lost units over the subject's base; a subject below the minimum base is left out of this ranking.</summary>
    Share = 1,
}
