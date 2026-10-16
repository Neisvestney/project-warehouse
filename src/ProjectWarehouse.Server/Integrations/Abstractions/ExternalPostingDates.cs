namespace ProjectWarehouse.Server.Integrations.Abstractions;

/// <summary>
/// Posting dates the marketplace states besides the creation and processing ones. All null for FBO.
/// </summary>
/// <param name="DeliveringAt">When the seller handed the posting over to delivery; kept after a cancellation.</param>
/// <param name="ShipmentDateWithoutDelay">The recommended ship-by time, past which a late tariff applies.</param>
/// <param name="DeliveryDateBegin">Start of the planned delivery window to the buyer.</param>
/// <param name="DeliveryDateEnd">End of the planned delivery window to the buyer.</param>
public record ExternalPostingDates(
    DateTime? DeliveringAt,
    DateTime? ShipmentDateWithoutDelay,
    DateTime? DeliveryDateBegin,
    DateTime? DeliveryDateEnd)
{
    public static readonly ExternalPostingDates None = new(null, null, null, null);
}
