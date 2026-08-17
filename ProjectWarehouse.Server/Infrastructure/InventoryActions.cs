namespace ProjectWarehouse.Server.Infrastructure;

/// <summary>Named action constants used in ChangeLog entries produced by InventoryService.</summary>
public static class InventoryActions
{
    public const string UnknownAction        = "inventory.unknown_action";
    public const string MoveStock            = "inventory.move_stock";
    public const string NewGoods             = "inventory.new_goods";
    public const string ReturnStock          = "inventory.return_stock";
    public const string WrittenOff           = "inventory.written_off";
    public const string SpentOnOrder         = "inventory.spent_on_order";
    public const string CancelledFulfillment = "inventory.canceled_fulfillment";
    public const string CancelledPlacement   = "inventory.canceled_placement";

    /// <summary>Stock found in excess of the books during a stocktake.</summary>
    public const string StocktakeSurplus     = "inventory.stocktake_surplus";

    /// <summary>Stock the books expected but the count did not find.</summary>
    public const string StocktakeShortage    = "inventory.stocktake_shortage";

    /// <summary>A unit item found in a cell other than the one the books placed it in.</summary>
    public const string StocktakeRelocation  = "inventory.stocktake_relocation";
}
