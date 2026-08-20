import {useCallback, useEffect, useLayoutEffect, useRef, useState} from "react";
import {realtimeAcquireLock, realtimeReleaseLock} from "@/api/sdk.gen";
import type {AppEntityType} from "@/api/types.gen";
import {useAuth} from "@/hooks/useAuth";
import {useRealtime} from "@/hooks/useRealtime";
import {useRealtimeEvent} from "@/hooks/useRealtimeEvent";
import {
  useStaleData,
  type StaleActor,
  type UseStaleDataOptions,
  type UseStaleDataResult,
} from "@/hooks/useStaleData";
import {firstFieldError, isAppProblemDetails} from "@/utils/errorUtils";

/** Retry cadence while somebody else holds the object; holding it needs no poll of its own. */
const RETRY_MS = 20_000;

export interface UseEditLockOptions extends Omit<UseStaleDataOptions, "enabled"> {
  /**
   * Gates the claim only — watching and the staleness warning stay on regardless. Pages with an
   * explicit edit mode pass it so that merely reading an object does not claim it.
   */
  enabled?: boolean;
}

export interface UseEditLockResult extends UseStaleDataResult {
  isOwner: boolean;
  heldBy: StaleActor | null;
  isLoading: boolean;
}

/**
 * Claims the object while the user edits it. The lock is advisory — nothing is disabled and saving is
 * never blocked; it only makes a collision visible before it happens.
 */
export function useEditLock(
  entityType: AppEntityType,
  entityId: string | null | undefined,
  {enabled = true, ...staleOptions}: UseEditLockOptions,
): UseEditLockResult {
  const {connectionId, heldLocks, presenceKey} = useRealtime();
  const {user} = useAuth();

  const [isOwner, setIsOwner] = useState(false);
  const [heldBy, setHeldBy] = useState<StaleActor | null>(null);
  const [isLoading, setIsLoading] = useState(false);

  const stale = useStaleData(entityType, entityId, staleOptions);

  const active = enabled && !!entityId && !!connectionId;

  // Mirrors the state for the closures below. Bound to `isOwner` rather than every render: unrelated
  // renders would otherwise undo a hand-off written straight to the ref.
  const isOwnerRef = useRef(false);
  useLayoutEffect(() => {
    isOwnerRef.current = isOwner;
  }, [isOwner]);

  const acquireRef = useRef<(() => void) | null>(null);
  const [acquiredAt, setAcquiredAt] = useState(0);

  useEffect(() => {
    if (!active) return;

    const body = {connectionId: connectionId!, entityType, entityId: entityId!};
    let disposed = false;

    const acquire = async () => {
      setIsLoading(true);
      const {error} = await realtimeAcquireLock({body});
      if (disposed) return;

      setIsLoading(false);
      if (error === undefined) {
        setAcquiredAt(Date.now());
        isOwnerRef.current = true;
        setIsOwner(true);
        setHeldBy(null);
        return;
      }

      isOwnerRef.current = false;
      setIsOwner(false);

      const held = isAppProblemDetails(error) ? firstFieldError(error) : undefined;
      const args = held?.code === "editLockHeld" ? held.args : undefined;
      setHeldBy(args ? {userId: String(args.userId), userName: String(args.userName)} : null);
    };

    acquireRef.current = () => void acquire();
    void acquire();

    // Keeping the lock is the connection heartbeat's job now; this only waits out the current holder.
    // A hidden tab stops asking: nobody is waiting to edit, and `retryOnVisible` catches up on return.
    const timer = window.setInterval(() => {
      if (document.visibilityState !== "visible") return;
      if (!isOwnerRef.current) void acquire();
    }, RETRY_MS);

    // The state goes too, not just the ref: coming back to the same object with stale ownership would
    // read as a takeover on the next heartbeat and fire a second acquire behind the first.
    const release = () => {
      if (!isOwnerRef.current) return;
      isOwnerRef.current = false;
      setIsOwner(false);
      void realtimeReleaseLock({body}).catch(() => {});
    };

    // A request started during unload is normally cancelled; `keepalive` is what lets it finish.
    // sendBeacon cannot carry the bearer header, so it is not an option here. Belt and braces anyway:
    // the stream drops too, and the server releases everything that connection held.
    const releaseOnUnload = () => {
      if (!isOwnerRef.current) return;
      isOwnerRef.current = false;

      const token = localStorage.getItem("accessToken");
      void fetch(`${window.location.origin}/api/realtime/locks/release`, {
        method: "POST",
        keepalive: true,
        headers: {
          "Content-Type": "application/json",
          ...(token ? {Authorization: `Bearer ${token}`} : {}),
        },
        body: JSON.stringify(body),
      }).catch(() => {});
    };

    // Release events only arrive while the stream is up, so a tab returning from the background has to
    // ask rather than assume the object is still taken.
    const retryOnVisible = () => {
      if (document.visibilityState !== "visible") return;
      if (!isOwnerRef.current) void acquire();
    };

    window.addEventListener("beforeunload", releaseOnUnload);
    document.addEventListener("visibilitychange", retryOnVisible);

    return () => {
      disposed = true;
      acquireRef.current = null;
      window.clearInterval(timer);
      window.removeEventListener("beforeunload", releaseOnUnload);
      document.removeEventListener("visibilitychange", retryOnVisible);
      release();
    };
    // A new connectionId means the server already dropped the old lock — re-acquire under the new one.
  }, [active, connectionId, entityType, entityId]);

  // Another tab of the same user takes the lock over silently — that raises no event this page would
  // accept, so the heartbeat no longer listing it is the only sign. `at` discards a reply that was
  // already in flight when the lock was taken.
  const takenOver =
    active &&
    isOwner &&
    heldLocks.at > acquiredAt &&
    !heldLocks.keys.has(presenceKey(entityType, entityId!));

  useEffect(() => {
    if (!takenOver) return;

    isOwnerRef.current = false;
    acquireRef.current?.();
  }, [takenOver]);

  const matches = useCallback(
    (payloadType: AppEntityType, payloadId: string) =>
      !!entityId && payloadType === entityType && payloadId === entityId,
    [entityId, entityType],
  );

  // Events are handled even while the lock is not being taken: a page in read mode should still say
  // who is editing the object.
  useRealtimeEvent("editLockAcquired", (_event, payload) => {
    if (!matches(payload.entityType, payload.entityId)) return;
    if (payload.userId === user?.id) return;

    isOwnerRef.current = false;
    setIsOwner(false);
    setHeldBy({userId: payload.userId, userName: payload.userName});
  });

  useRealtimeEvent("editLockReleased", (_event, payload) => {
    if (!matches(payload.entityType, payload.entityId)) return;
    if (payload.userId === user?.id) return;

    setHeldBy(null);
    // The object just became free — take it rather than waiting for the next heartbeat tick.
    if (!isOwnerRef.current) acquireRef.current?.();
  });

  return {...stale, isOwner: active && isOwner && !takenOver, heldBy, isLoading};
}
