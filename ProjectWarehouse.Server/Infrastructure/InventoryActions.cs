namespace ProjectWarehouse.Server.Infrastructure;

/// <summary>Named action constants used in ChangeLog entries produced by InventoryService.</summary>
public static class InventoryActions
{
    public const string AddStandardItems      = "inventory.add_standard";
    public const string RemoveStandardItems   = "inventory.remove_standard";
    public const string AddUnitItem           = "inventory.add_unit";
    public const string RemoveUnitItem        = "inventory.remove_unit";
    public const string AddAssembledBundle    = "inventory.add_assembled_bundle";
    public const string RemoveAssembledBundle = "inventory.remove_assembled_bundle";
    public const string MoveStandardItems     = "inventory.move_standard";
    public const string MoveUnitItem          = "inventory.move_unit";
}
