namespace ProjectWarehouse.Server.Infrastructure;

/// <summary>Named action constants used in ChangeLog entries produced by TransfersController.</summary>
public static class TransferActions
{
    public const string TransferStandard        = "transfer.standard";
    public const string TransferUnit            = "transfer.unit";
    public const string TransferAssembledBundle = "transfer.assembled_bundle";
}
