namespace ProjectWarehouse.Server.Models.Receipts;

public enum ReceiptTransition
{
    Plan = 0,
    StartProcessing = 1,
    Finish = 2,
    Revert = 3,
    Cancel = 4,
}
