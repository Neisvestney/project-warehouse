namespace ProjectWarehouse.Server.Domain;

/// <summary>
/// Which way stock crossed the boundary of a node. Transfers are kept apart from real receipts and
/// issues: a move between two nodes produces a <see cref="TransferOut"/> and a <see cref="TransferIn"/>
/// row, and counting those as In/Out would inflate both totals for stock that never left the company.
/// </summary>
public enum StockMovementDirection
{
    In = 1,
    Out = 2,
    TransferIn = 3,
    TransferOut = 4,
}
