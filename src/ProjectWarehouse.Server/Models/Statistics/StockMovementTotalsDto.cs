namespace ProjectWarehouse.Server.Models.Statistics;

/// <summary>Quantities summed per direction. <see cref="Net"/> counts transfers too, because at node
/// or storage-place level a transfer really does change what is on the shelf.</summary>
public class StockMovementTotalsDto
{
    public int InQuantity { get; init; }
    public int OutQuantity { get; init; }
    public int TransferInQuantity { get; init; }
    public int TransferOutQuantity { get; init; }
    public int MovementsCount { get; init; }

    public int Net => InQuantity + TransferInQuantity - OutQuantity - TransferOutQuantity;
}
