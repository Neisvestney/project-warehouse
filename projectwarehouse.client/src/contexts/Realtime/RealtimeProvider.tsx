import {type ReactNode, useCallback, useEffect, useMemo, useRef, useState} from "react";
import {realtimeStream, realtimeUnwatch, realtimeWatch} from "@/api/sdk.gen";
import type {
  AppEntityType,
  RealtimeEvent,
  RealtimeEventPayloadConnectionReadyPayload,
  RealtimeEventPayloadEntityPresenceChangedPayload,
  RealtimeEventType,
  RealtimeViewer,
} from "@/api/types.gen";
import RealtimeContext, {type RealtimeContextValue} from "@/contexts/Realtime/RealtimeContext";

const INITIAL_RETRY_MS = 1000;
const MAX_RETRY_MS = 30_000;

type EventHandler = (event: RealtimeEvent) => void;

interface WatchTarget {
  entityType: AppEntityType;
  entityId: string;
}

interface WatchEntry extends WatchTarget {
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
  const [presence, setPresence] = useState<ReadonlyMap<string, readonly RealtimeViewer[]>>(
    () => new Map(),
  );

  const connectionIdRef = useRef<string | null>(null);
  const handlersRef = useRef(new Map<RealtimeEventType, Set<EventHandler>>());
  const watchesRef = useRef(new Map<string, WatchEntry>());

  const sendWatch = useCallback(async (entries: WatchEntry[]) => {
    const connection = connectionIdRef.current;
    // An entry unregistered within the same batch is dropped here: sending it would race its unwatch.
    const pending = entries.filter(
      (e) =>
        watchesRef.current.get(watchKey(e.entityType, e.entityId)) === e &&
        !e.confirmed &&
        e.pendingFor !== connection,
    );
    if (!connection || pending.length === 0) return;

    pending.forEach((e) => (e.pendingFor = connection));
    try {
      const {data, error} = await realtimeWatch({
        body: {
          connectionId: connection,
          entities: pending.map((e) => ({entityType: e.entityType, entityId: e.entityId})),
        },
      });
      // 422 means the connection already died; a fresh one re-sends the whole batch. Entities the user
      // may not view are simply missing from `watched`, and those pages keep their polling fallback.
      if (error !== undefined || data === undefined) return;
      if (connectionIdRef.current !== connection) return;

      const confirmed: string[] = [];
      for (const {entityType, entityId} of data.watched) {
        const key = watchKey(entityType, entityId);
        const entry = watchesRef.current.get(key);
        if (!entry || entry.confirmed) continue;

        entry.confirmed = true;
        confirmed.push(key);
        entry.callbacks.forEach((cb) => cb());
      }

      if (confirmed.length > 0) {
        setConfirmedKeys((prev) => {
          const next = new Set(prev);
          confirmed.forEach((key) => next.add(key));
          return next;
        });
      }

      // Seeding from the response closes the window between subscribing and the first presence event.
      setPresence((prev) => {
        const next = new Map(prev);
        for (const {entityType, entityId, viewers} of data.presence) {
          const key = watchKey(entityType, entityId);
          if (watchesRef.current.has(key)) next.set(key, viewers);
        }
        return next;
      });
    } catch {
      // Network failure — the stream is about to drop too, and reconnecting re-sends the watch.
    } finally {
      // A newer connection may already have claimed an entry while this request was in flight.
      pending.forEach((e) => {
        if (e.pendingFor === connection) e.pendingFor = null;
      });
    }
  }, []);

  const flushWatches = useCallback(() => {
    void sendWatch([...watchesRef.current.values()]);
  }, [sendWatch]);

  // Mounting a screen registers one watch per visible object; sending them separately would open a
  // request each and the browser only allows six per origin. A microtask is enough to collect a render.
  const queuedRef = useRef<{watch: Set<WatchEntry>; unwatch: WatchTarget[]} | null>(null);

  const enqueue = useCallback(
    (op: "watch" | "unwatch", target: WatchEntry | WatchTarget) => {
      const queue = queuedRef.current ?? {watch: new Set<WatchEntry>(), unwatch: []};
      if (queuedRef.current === null) {
        queuedRef.current = queue;
        queueMicrotask(() => {
          const {watch: toWatch, unwatch: toUnwatch} = queue;
          queuedRef.current = null;

          if (toWatch.size > 0) void sendWatch([...toWatch]);

          const connection = connectionIdRef.current;
          if (connection && toUnwatch.length > 0) {
            void realtimeUnwatch({
              body: {connectionId: connection, entities: toUnwatch},
            }).catch(() => {});
          }
        });
      }

      if (op === "watch") queue.watch.add(target as WatchEntry);
      else queue.unwatch.push(target as WatchTarget);
    },
    [sendWatch],
  );

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
        enqueue("watch", registered);
      }

      return () => {
        const current = watchesRef.current.get(key);
        if (current !== registered) return;

        current.callbacks.delete(onWatched);
        current.refCount--;
        if (current.refCount > 0) return;

        watchesRef.current.delete(key);
        setPresence((prev) => {
          if (!prev.has(key)) return prev;
          const next = new Map(prev);
          next.delete(key);
          return next;
        });
        setConfirmedKeys((prev) => {
          if (!prev.has(key)) return prev;
          const next = new Set(prev);
          next.delete(key);
          return next;
        });

        if (connectionIdRef.current) enqueue("unwatch", {entityType, entityId});
      };
    },
    [enqueue],
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

  const applyPresence = useCallback((payload: RealtimeEventPayloadEntityPresenceChangedPayload) => {
    const key = watchKey(payload.entityType, payload.entityId);
    setPresence((prev) => {
      // An event for an object nobody on this tab watches any more would leak the entry back in.
      if (!watchesRef.current.has(key)) return prev;
      const next = new Map(prev);
      next.set(key, payload.viewers);
      return next;
    });
  }, []);

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
      setPresence(new Map());
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
            if (event.type === "entityPresenceChanged") {
              applyPresence(event.payload as RealtimeEventPayloadEntityPresenceChangedPayload);
            }
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
  }, [applyPresence, flushWatches]);

  const value = useMemo<RealtimeContextValue>(
    () => ({
      connectionId,
      isConnected: connectionId !== null,
      subscribe,
      watch,
      isWatching,
      presence,
      presenceKey: watchKey,
    }),
    [connectionId, subscribe, watch, isWatching, presence],
  );

  return <RealtimeContext.Provider value={value}>{children}</RealtimeContext.Provider>;
}
