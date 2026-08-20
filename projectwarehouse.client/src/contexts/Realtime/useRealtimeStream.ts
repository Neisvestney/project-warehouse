import {useEffect} from "react";
import {realtimeStream} from "@/api/sdk.gen";
import type {RealtimeEvent, RealtimeEventPayloadConnectionReadyPayload} from "@/api/types.gen";

const INITIAL_RETRY_MS = 1000;
const MAX_RETRY_MS = 30_000;
const IDLE_UNWATCH_MS = 20_000;
const IDLE_DISCONNECT_MS = 120_000;

export interface RealtimeStreamHandlers {
  /** Every event but `connectionReady`, which the stream keeps to itself. */
  onEvent: (event: RealtimeEvent) => void;
  onConnected: (connectionId: string) => void;
  onDisconnected: () => void;
  /** Hidden past the first threshold with no locks held — drop the subscriptions, keep the stream. */
  onIdle: () => void;
  onResumed: () => void;
  /** Answers whether the server still lists a lock for this tab; `null` when it did not answer. */
  holdsLocks: () => Promise<boolean | null>;
}

/**
 * Owns the only stream in the app: the connect loop, its backoff, and the two idle thresholds a
 * backgrounded tab falls through. Knows nothing about what the events mean — that is the provider's job.
 *
 * Reconnection is hand-rolled because the generated SSE client exits its loop when the server closes the
 * stream cleanly, which is exactly what happens every time the access token expires.
 */
export function useRealtimeStream(handlers: RealtimeStreamHandlers) {
  const {onEvent, onConnected, onDisconnected, onIdle, onResumed, holdsLocks} = handlers;

  useEffect(() => {
    let disposed = false;
    let stopped = false;
    let idleStopped = false;
    let retryDelay = INITIAL_RETRY_MS;
    let retryTimer: number | null = null;
    let idleWatchTimer: number | null = null;
    let idleStreamTimer: number | null = null;
    let controller: AbortController | null = null;

    const clearRetryTimer = () => {
      if (retryTimer === null) return;
      clearTimeout(retryTimer);
      retryTimer = null;
    };

    const scheduleReconnect = () => {
      if (disposed || stopped || idleStopped || retryTimer !== null) return;
      const delay = retryDelay + Math.random() * 300;
      retryDelay = Math.min(retryDelay * 2, MAX_RETRY_MS);
      retryTimer = window.setTimeout(() => {
        retryTimer = null;
        void connect();
      }, delay);
    };

    const connect = async () => {
      if (disposed || stopped || idleStopped || controller) return;
      if (!localStorage.getItem("accessToken")) {
        scheduleReconnect();
        return;
      }

      const current = new AbortController();
      controller = current;

      try {
        const {stream} = await realtimeStream({signal: current.signal, sseMaxRetryAttempts: 1});

        for await (const event of stream) {
          if (event.type === "connectionReady") {
            retryDelay = INITIAL_RETRY_MS;
            const {connectionId} = event.payload as RealtimeEventPayloadConnectionReadyPayload;
            onConnected(connectionId);
          } else {
            onEvent(event);
          }
        }
      } catch {
        // Request setup failed; treated the same as a dropped stream.
      }

      controller = null;
      if (disposed) return;

      onDisconnected();
      // No `signal.aborted` check: every deliberate abort already sets one of the flags
      // `scheduleReconnect` guards on. Testing the signal instead would strand the stream when the tab
      // comes back during the unwind of an idle abort — the flag is cleared by then, the loop is not.
      scheduleReconnect();
    };

    const reconnectNow = () => {
      if (disposed || stopped || idleStopped || controller) return;
      clearRetryTimer();
      retryDelay = INITIAL_RETRY_MS;
      void connect();
    };

    const stop = () => {
      stopped = true;
      clearRetryTimer();
      controller?.abort();
    };

    const clearIdleTimers = () => {
      if (idleWatchTimer !== null) clearTimeout(idleWatchTimer);
      if (idleStreamTimer !== null) clearTimeout(idleStreamTimer);
      idleWatchTimer = idleStreamTimer = null;
    };

    // A hidden tab has nobody looking at it, but the server still counts it as a viewer and keeps its
    // locks. Two steps out: subscriptions first, then the stream itself — which is what frees the locks.
    const startIdleTimers = () => {
      clearIdleTimers();

      idleWatchTimer = window.setTimeout(() => {
        idleWatchTimer = null;
        // Holding a lock means the tab is still the editor of something. Unsubscribing it would blind
        // it to changes on an object it is about to save over, so it keeps watching until the stream
        // goes. The heartbeat answer is the truth here — locally held state can be a cycle behind.
        void holdsLocks().then((holds) => {
          if (holds !== false || document.visibilityState === "visible") return;
          onIdle();
        });
      }, IDLE_UNWATCH_MS);

      idleStreamTimer = window.setTimeout(() => {
        idleStreamTimer = null;
        idleStopped = true;
        clearRetryTimer();
        // Nothing to unwatch by hand: dropping the stream is what makes the server release everything
        // the connection held, subscriptions and locks alike, and `onDisconnected` clears the rest.
        controller?.abort();
      }, IDLE_DISCONNECT_MS);
    };

    // Only the stream is restored here: TanStack's own focusManager/onlineManager already refetch
    // every active query on these same two events, and staleTime is 0 across the app.
    const onVisibilityChange = () => {
      if (document.visibilityState !== "visible") {
        startIdleTimers();
        return;
      }

      clearIdleTimers();
      idleStopped = false;
      onResumed();
      reconnectNow();
    };

    window.addEventListener("auth:clear", stop);
    window.addEventListener("auth:refreshTokenInvalid", stop);
    document.addEventListener("visibilitychange", onVisibilityChange);
    window.addEventListener("online", reconnectNow);

    void connect();
    if (document.visibilityState !== "visible") startIdleTimers();

    return () => {
      disposed = true;
      stop();
      clearIdleTimers();
      window.removeEventListener("auth:clear", stop);
      window.removeEventListener("auth:refreshTokenInvalid", stop);
      document.removeEventListener("visibilitychange", onVisibilityChange);
      window.removeEventListener("online", reconnectNow);
    };
  }, [onEvent, onConnected, onDisconnected, onIdle, onResumed, holdsLocks]);
}
