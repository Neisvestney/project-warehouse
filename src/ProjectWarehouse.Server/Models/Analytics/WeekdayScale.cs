namespace ProjectWarehouse.Server.Models.Analytics;

public enum WeekdayScale
{
    /// <summary>Average per occurrence of the weekday.</summary>
    Average = 0,

    /// <summary>Median over the full weeks of the weekday's share of its week.</summary>
    WeekShare = 1,
}
