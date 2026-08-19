namespace ProjectWarehouse.Server.Infrastructure.Realtime;

public enum RealtimeEventType
{
    ConnectionReady,
    MarketplaceSyncProgress,
    MarketplaceSyncFinished,
    EntityChanged,
    EditLockAcquired,
    EditLockReleased,
    EntityPresenceChanged,
}
