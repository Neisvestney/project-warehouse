import {type ReactNode, useCallback, useEffect, useMemo, useRef, useState} from "react";
import {realtimeStream, realtimeUnwatch, realtimeWatch} from "@/api/sdk.gen";
import type {
  AppEntityType,
  RealtimeEvent,
  RealtimeEventPayloadConnectionReadyPayload,
  RealtimeEventType,
} from "@/api/types.gen";
import RealtimeContext, {type RealtimeContextValue} from "@/contexts/Realtime/RealtimeContext";

const INITIAL_RETRY_MS = 1000;
const MAX_RETRY_MS = 30_000;

type EventHandler = (event: RealtimeEvent) => void;

interface WatchEntry {
  entityType: AppEntityType;
  entityId: string;
  refCount: number;
  confirmed: boolean;
  /** Connection a `watch` is in flight for — a boolean here would block re-sending on reconnect. */
  pendingFor: string | null;
  callbacks: Set<() => void>;
}

const watchKey = (entityType: AppEntityType, entityId: string) => `${entityType}:${entityId}`;

export function RealtimeProvider({children}: {children: ReactNode}) {
  const [connectionId, setConnectionId] = useState<string | null>(null);
  const [confirmedKeys, setConfirmedKeys] = useState<ReadonlySet<string>>(() => new Set());

  const connectionIdRef = useRef<string | null>(null);
  const handlersRef = useRef(new Map<RealtimeEventType, Set<EventHandler>>());
  const watchesRef = useRef(new Map<string, WatchEntry>());

  const sendWatch = useCallback(async (entry: WatchEntry) => {
    const connection = connectionIdRef.current;
    if (!connection || entry.confirmed || entry.pendingFor === connection) return;

    entry.pendingFor = connection;
    try {
      const {error} = await realtimeWatch({
        body: {connectionId: connection, entityType: entry.entityType, entityId: entry.entityId},
      });
      // 403 means no view permission, 422 means the connection already died — in both cases the
      // page keeps its polling fallback, and a fresh connection re-sends the watch.
      if (error !== undefined) return;
      if (connectionIdRef.current !== connection) return;

      const key = watchKey(entry.entityType, entry.entityId);
      if (watchesRef.current.get(key) !== entry) return;

      entry.confirmed = true;
      setConfirmedKeys((prev) => new Set(prev).add(key));
      entry.callbacks.forEach((cb) => cb());
    } catch {
      // Network failure — the stream is about to drop too, and reconnecting re-sends the watch.
    } finally {
      // A newer connection may already have claimed the entry while this request was in flight.
      if (entry.pendingFor === connection) entry.pendingFor = null;
    }
  }, []);

  const flushWatches = useCallback(() => {
    for (const entry of watchesRef.current.values()) {
      void sendWatch(entry);
    }
  }, [sendWatch]);

  const watch = useCallback<RealtimeContextValue["watch"]>(
    (entityType, entityId, onWatched) => {
      const key = watchKey(entityType, entityId);
      let entry = watchesRef.current.get(key);
      if (!entry) {
        entry = {
          entityType,
          entityId,
          refCount: 0,
          confirmed: false,
          pendingFor: null,
          callbacks: new Set(),
        };
        watchesRef.current.set(key, entry);
      }
      const registered = entry;
      registered.refCount++;
      registered.callbacks.add(onWatched);

      if (registered.confirmed) {
        // Joining an already-registered watch still means "subscribed → refetch" for this consumer.
        onWatched();
      } else {
        void sendWatch(registered);
      }

      return () => {
        const current = watchesRef.current.get(key);
        if (current !== registered) return;

        current.callbacks.delete(onWatched);
        current.refCount--;
        if (current.refCount > 0) return;

        watchesRef.current.delete(key);
        setConfirmedKeys((prev) => {
          if (!prev.has(key)) return prev;
          const next = new Set(prev);
          next.delete(key);
          return next;
        });

        const connection = connectionIdRef.current;
        if (connection) {
          void realtimeUnwatch({body: {connectionId: connection, entityType, entityId}}).catch(
            () => {},
          );
        }
      };
    },
    [sendWatch],
  );

  const subscribe = useCallback<RealtimeContextValue["subscribe"]>((type, handler) => {
    let handlers = handlersRef.current.get(type);
    if (!handlers) {
      handlers = new Set();
      handlersRef.current.set(type, handlers);
    }
    handlers.add(handler);

    return () => {
      handlersRef.current.get(type)?.delete(handler);
    };
  }, []);

  const isWatching = useCallback<RealtimeContextValue["isWatching"]>(
    (entityType, entityId) => confirmedKeys.has(watchKey(entityType, entityId)),
    [confirmedKeys],
  );

  useEffect(() => {
    let disposed = false;
    let stopped = false;
    let retryDelay = INITIAL_RETRY_MS;
    let retryTimer: number | null = null;
    let controller: AbortController | null = null;

    const dispatch = (event: RealtimeEvent) => {
      const handlers = handlersRef.current.get(event.type);
      if (!handlers) return;
      for (const handler of [...handlers]) {
        try {
          handler(event);
        } catch (e) {
          console.error("Realtime handler failed", e);
        }
      }
    };

    const dropConnection = () => {
      connectionIdRef.current = null;
      setConnectionId(null);
      setConfirmedKeys(new Set());
      for (const entry of watchesRef.current.values()) {
        entry.confirmed = false;
      }
    };

    const scheduleReconnect = () => {
      if (disposed || stopped || retryTimer !== null) return;
      const delay = retryDelay + Math.random() * 300;
      retryDelay = Math.min(retryDelay * 2, MAX_RETRY_MS);
      retryTimer = window.setTimeout(() => {
        retryTimer = null;
        void connect();
      }, delay);
    };

    // The generated SSE client stops its own loop when the server closes the stream cleanly —
    // exactly what happens when the access token expires — so reconnection lives here instead.
    const connect = async () => {
      if (disposed || stopped || controller) return;
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
            const {connectionId: id} = event.payload as RealtimeEventPayloadConnectionReadyPayload;
            connectionIdRef.current = id;
            setConnectionId(id);
            flushWatches();
          } else {
            dispatch(event);
          }
        }
      } catch {
        // Request setup failed; treated the same as a dropped stream.
      }

      controller = null;
      if (disposed) return;

      dropConnection();
      if (!current.signal.aborted) scheduleReconnect();
    };

    const reconnectNow = () => {
      if (disposed || stopped || controller) return;
      if (retryTimer !== null) {
        clearTimeout(retryTimer);
        retryTimer = null;
      }
      retryDelay = INITIAL_RETRY_MS;
      void connect();
    };

    const stop = () => {
      stopped = true;
      if (retryTimer !== null) {
        clearTimeout(retryTimer);
        retryTimer = null;
      }
      controller?.abort();
    };

    // Only the stream is restored here: TanStack's own focusManager/onlineManager already refetch
    // every active query on these same two events, and staleTime is 0 across the app.
    const onVisibilityChange = () => {
      if (document.visibilityState === "visible") reconnectNow();
    };

    window.addEventListener("auth:clear", stop);
    window.addEventListener("auth:refreshTokenInvalid", stop);
    document.addEventListener("visibilitychange", onVisibilityChange);
    window.addEventListener("online", reconnectNow);

    void connect();

    return () => {
      disposed = true;
      stop();
      window.removeEventListener("auth:clear", stop);
      window.removeEventListener("auth:refreshTokenInvalid", stop);
      document.removeEventListener("visibilitychange", onVisibilityChange);
      window.removeEventListener("online", reconnectNow);
    };
  }, [flushWatches]);

  const value = useMemo<RealtimeContextValue>(
    () => ({connectionId, isConnected: connectionId !== null, subscribe, watch, isWatching}),
    [connectionId, subscribe, watch, isWatching],
  );

  return <RealtimeContext.Provider value={value}>{children}</RealtimeContext.Provider>;
}
