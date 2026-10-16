import {useCallback, useEffect, useRef} from "react";
import {realtimeHeartbeat} from "@/api/sdk.gen";

const HEARTBEAT_MS = 20_000;

/**
 * The stream alone does not prove the tab is alive: a proxy between the browser and the server keeps
 * accepting writes for a tab that is gone, so without this the server never releases its locks or
 * presence. Beats every 20 s, and answers whether this connection is still editing anything.
 */
export function useHeartbeat(connectionId: string | null) {
  // Keeps the exported `beat` stable while its implementation is replaced on every new connection.
  const beatRef = useRef<(() => Promise<boolean | null>) | null>(null);

  useEffect(() => {
    if (!connectionId) return;

    let disposed = false;

    const beat = async (): Promise<boolean | null> => {
      const {data, error} = await realtimeHeartbeat({body: {connectionId}});
      if (disposed || error !== undefined || data === undefined) return null;
      return data.holdsLocks;
    };

    beatRef.current = beat;
    void beat();
    const timer = window.setInterval(() => void beat(), HEARTBEAT_MS);

    // Background tabs get their timers throttled to a minute or more, which can outlive the server's
    // TTL — coming back has to report in at once rather than wait for the next tick.
    const onVisible = () => {
      if (document.visibilityState === "visible") void beat();
    };
    document.addEventListener("visibilitychange", onVisible);

    return () => {
      disposed = true;
      if (beatRef.current === beat) beatRef.current = null;
      window.clearInterval(timer);
      document.removeEventListener("visibilitychange", onVisible);
    };
  }, [connectionId]);

  // A thrown request is the same answer as a refused one: nothing was learned about the locks.
  return useCallback(async () => {
    try {
      return (await beatRef.current?.()) ?? null;
    } catch {
      return null;
    }
  }, []);
}
