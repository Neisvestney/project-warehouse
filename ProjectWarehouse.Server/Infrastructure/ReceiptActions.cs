namespace ProjectWarehouse.Server.Infrastructure;

/// <summary>Named action constants used in ChangeLog entries produced by ReceiptsController.</summary>
public static class ReceiptActions
{
    public const string ItemsSynced          = "items_synced";
    public const string ItemQuickAdded       = "item_quick_added";
    public const string ReceivedCountUpdated = "received_count_updated";
    public const string PlacementAdded       = "placement_added";
    public const string BatchPlacementsAdded = "batch_placements_added";
    public const string PlacementRemoved     = "placement_removed";
    public const string Planned              = "planned";
    public const string ProcessingStarted    = "processing_started";
    public const string Finished             = "finished";
    public const string Reverted             = "reverted";
    public const string Canceled             = "canceled";
}
