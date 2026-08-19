namespace ProjectWarehouse.Server.Infrastructure.Realtime;

public class RealtimeEvent
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public DateTime At { get; init; } = DateTime.UtcNow;

    public required RealtimeEventPayload Payload { get; init; }

    /// <summary>Derived from the payload so the envelope and the discriminator cannot disagree.</summary>
    public RealtimeEventType Type => Payload.Type;
}
