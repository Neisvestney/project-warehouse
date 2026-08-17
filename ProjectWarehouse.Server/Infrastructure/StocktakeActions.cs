namespace ProjectWarehouse.Server.Infrastructure;

/// <summary>Named action constants used in ChangeLog entries produced by StocktakesController.</summary>
public static class StocktakeActions
{
    public const string NodesSynced = "nodes_synced";
    public const string ItemsSynced = "items_synced";
    public const string Started     = "started";
    public const string Reverted    = "reverted";
    public const string Finished    = "finished";
    public const string Canceled    = "canceled";
}
