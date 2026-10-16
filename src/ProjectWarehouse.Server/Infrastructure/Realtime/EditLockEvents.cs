namespace ProjectWarehouse.Server.Infrastructure.Realtime;

public static class EditLockEvents
{
    /// <summary>
    /// The holder is excluded: their own page learns the outcome from the acquire response, and a second
    /// tab of the same person is the new holder anyway.
    /// </summary>
    public static ValueTask PublishLockAcquiredAsync(this IRealtimeNotifier notifier, EditLock @lock,
        CancellationToken ct = default) =>
        notifier.PublishAsync(RealtimeAddress.ToWatchers(@lock.EntityType, @lock.EntityId, @lock.UserId),
            new RealtimeEvent
            {
                Payload = new EditLockAcquiredPayload
                {
                    EntityType = @lock.EntityType,
                    EntityId = @lock.EntityId,
                    UserId = @lock.UserId,
                    UserName = @lock.UserName,
                },
            }, ct);

    /// <summary>
    /// Nobody is excluded here: a lock dropped by a broken stream has to reach the holder's
    /// remaining tabs too, and the client tells its own release apart by the user id in the payload.
    /// </summary>
    public static ValueTask PublishLockReleasedAsync(this IRealtimeNotifier notifier, EditLock @lock,
        CancellationToken ct = default) =>
        notifier.PublishAsync(RealtimeAddress.ToWatchers(@lock.EntityType, @lock.EntityId), new RealtimeEvent
        {
            Payload = new EditLockReleasedPayload
            {
                EntityType = @lock.EntityType,
                EntityId = @lock.EntityId,
                UserId = @lock.UserId,
                UserName = @lock.UserName,
            },
        }, ct);
}
