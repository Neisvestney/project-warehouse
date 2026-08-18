namespace ProjectWarehouse.Server.Domain;

public enum StocktakeStatus
{
    Draft = 0,
    /// <summary>Same capabilities as <see cref="Draft"/>; marks a scheduled document not yet taken into work.</summary>
    Planned = 4,
    InProgress = 1,
    Finished = 2,
    Canceled = 3,
}
