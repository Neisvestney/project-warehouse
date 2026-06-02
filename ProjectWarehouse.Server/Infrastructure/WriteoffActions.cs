namespace ProjectWarehouse.Server.Infrastructure;

/// <summary>Named action constants used in ChangeLog entries produced by WriteoffsController.</summary>
public static class WriteoffActions
{
    public const string ItemsSynced = "items_synced";
    public const string Finished    = "finished";
    public const string Canceled    = "canceled";
}
