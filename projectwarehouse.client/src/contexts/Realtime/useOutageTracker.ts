import {useCallback, useRef} from "react";

const OUTAGE_MS = 10_000;

export interface OutageTracker {
  /** Subscribes to reconnections that followed a gap long enough to be a server outage. */
  onReconnectedAfterOutage: (handler: () => void) => () => void;
  markDisconnected: () => void;
  markConnected: () => void;
}

/**
 * Times the gap between losing the stream and getting it back. A token expiry closes the stream
 * cleanly and the next attempt lands within a second; a restarted server is unreachable for as long
 * as the container takes to come up, which is what `OUTAGE_MS` separates.
 *
 * The first connect of a page load reports nothing — there was no gap to measure.
 */
export function useOutageTracker(): OutageTracker {
  const disconnectedAtRef = useRef<number | null>(null);
  const handlersRef = useRef(new Set<() => void>());

  const onReconnectedAfterOutage = useCallback((handler: () => void) => {
    handlersRef.current.add(handler);
    return () => {
      handlersRef.current.delete(handler);
    };
  }, []);

  const markDisconnected = useCallback(() => {
    disconnectedAtRef.current = Date.now();
  }, []);

  const markConnected = useCallback(() => {
    const disconnectedAt = disconnectedAtRef.current;
    disconnectedAtRef.current = null;
    if (disconnectedAt === null || Date.now() - disconnectedAt < OUTAGE_MS) return;

    for (const handler of [...handlersRef.current]) {
      try {
        handler();
      } catch (e) {
        console.error("Outage handler failed", e);
      }
    }
  }, []);

  return {onReconnectedAfterOutage, markDisconnected, markConnected};
}
