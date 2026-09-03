namespace ProjectWarehouse.Server.Domain;

/// <summary>
/// Origin of a stock change, passed down to <c>InventoryService</c> so the journal row can point back at
/// the document that caused it. Every field is optional — a movement made outside any document carries none.
/// </summary>
public record StockMovementContext(Guid? ReceiptId = null);
