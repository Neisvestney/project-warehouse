import {useCallback, useEffect, useLayoutEffect, useRef, useState} from "react";
import {realtimeAcquireLock, realtimeHeartbeatLock, realtimeReleaseLock} from "@/api/sdk.gen";
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

const HEARTBEAT_MS = 20_000;

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
  const {connectionId} = useRealtime();
  const {user} = useAuth();

  const [isOwner, setIsOwner] = useState(false);
  const [heldBy, setHeldBy] = useState<StaleActor | null>(null);
  const [isLoading, setIsLoading] = useState(false);

  const stale = useStaleData(entityType, entityId, staleOptions);

  const active = enabled && !!entityId && !!connectionId;

  const isOwnerRef = useRef(false);
  useLayoutEffect(() => {
    isOwnerRef.current = isOwner;
  });

  const acquireRef = useRef<(() => void) | null>(null);

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

    // A failed heartbeat means the lock expired or moved elsewhere; re-acquiring is what recovers it.
    const timer = window.setInterval(() => {
      if (!isOwnerRef.current) {
        void acquire();
        return;
      }
      void realtimeHeartbeatLock({body}).then(({error}) => {
        if (!disposed && error !== undefined) void acquire();
      });
    }, HEARTBEAT_MS);

    const release = () => {
      if (!isOwnerRef.current) return;
      isOwnerRef.current = false;
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

    // Background tabs get their timers throttled to a minute or more, which outlives the 60 s TTL —
    // coming back has to renew immediately rather than wait for the next tick.
    const renewOnVisible = () => {
      if (document.visibilityState !== "visible") return;
      if (!isOwnerRef.current) {
        void acquire();
        return;
      }
      void realtimeHeartbeatLock({body}).then(({error}) => {
        if (!disposed && error !== undefined) void acquire();
      });
    };

    window.addEventListener("beforeunload", releaseOnUnload);
    document.addEventListener("visibilitychange", renewOnVisible);

    return () => {
      disposed = true;
      acquireRef.current = null;
      window.clearInterval(timer);
      window.removeEventListener("beforeunload", releaseOnUnload);
      document.removeEventListener("visibilitychange", renewOnVisible);
      release();
    };
    // A new connectionId means the server already dropped the old lock — re-acquire under the new one.
  }, [active, connectionId, entityType, entityId]);

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

  return {...stale, isOwner: active && isOwner, heldBy, isLoading};
}
