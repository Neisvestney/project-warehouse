import {type ReactNode, useCallback, useEffect, useMemo, useRef, useState} from "react";
import {realtimeHeartbeat, realtimeStream, realtimeUnwatch, realtimeWatch} from "@/api/sdk.gen";
import type {
  AppEntityType,
  RealtimeEvent,
  RealtimeEventPayloadConnectionReadyPayload,
  RealtimeEventPayloadEntityPresenceChangedPayload,
  RealtimeEventType,
  RealtimeViewer,
} from "@/api/types.gen";
import RealtimeContext, {
  type HeldLocks,
  type RealtimeContextValue,
} from "@/contexts/Realtime/RealtimeContext";

const INITIAL_RETRY_MS = 1000;
const MAX_RETRY_MS = 30_000;
const HEARTBEAT_MS = 20_000;
const IDLE_UNWATCH_MS = 20_000;
const IDLE_DISCONNECT_MS = 120_000;

const NO_LOCKS: HeldLocks = {keys: new Set(), at: 0};

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
  const [heldLocks, setHeldLocks] = useState<HeldLocks>(NO_LOCKS);

  const connectionIdRef = useRef<string | null>(null);
  const handlersRef = useRef(new Map<RealtimeEventType, Set<EventHandler>>());
  const watchesRef = useRef(new Map<string, WatchEntry>());
  const watchesPausedRef = useRef(false);
  /** Set by the heartbeat effect; resolves to whether the server still lists a lock for this tab. */
  const beatRef = useRef<(() => Promise<boolean | null>) | null>(null);

  const sendWatch = useCallback(async (entries: WatchEntry[]) => {
    if (watchesPausedRef.current) return;
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
      if (connectionIdRef.current !== connection || watchesPausedRef.current) return;

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
    watchesPausedRef.current = false;
    void sendWatch([...watchesRef.current.values()]);
  }, [sendWatch]);

  // A backgrounded tab drops its subscriptions but keeps the entries: the pages behind it are still
  // mounted, and coming back re-registers everything as if the stream had reconnected.
  const pauseWatches = useCallback(({notifyServer}: {notifyServer: boolean}) => {
    if (watchesPausedRef.current) return;
    watchesPausedRef.current = true;

    const entries = [...watchesRef.current.values()];
    for (const entry of entries) {
      entry.confirmed = false;
      entry.pendingFor = null;
    }
    setConfirmedKeys(new Set());
    setPresence(new Map());

    const connection = connectionIdRef.current;
    if (!notifyServer || !connection || entries.length === 0) return;
    void realtimeUnwatch({
      body: {
        connectionId: connection,
        entities: entries.map((e) => ({entityType: e.entityType, entityId: e.entityId})),
      },
    }).catch(() => {});
  }, []);

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

          // Navigating between two pages on the same object unwatches it and watches it right back
          // within one tick. The server keeps one subscription per connection, so sending both would
          // race, and an unwatch landing second drops the subscription the watch had just made.
          const connection = connectionIdRef.current;
          const dropped = toUnwatch.filter(
            (e) => !watchesRef.current.has(watchKey(e.entityType, e.entityId)),
          );
          if (connection && dropped.length > 0) {
            void realtimeUnwatch({
              body: {connectionId: connection, entities: dropped},
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
    let idleStopped = false;
    let retryDelay = INITIAL_RETRY_MS;
    let retryTimer: number | null = null;
    let idleWatchTimer: number | null = null;
    let idleStreamTimer: number | null = null;
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

    // The generated SSE client stops its own loop when the server closes the stream cleanly —
    // exactly what happens when the access token expires — so reconnection lives here instead.
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
        void beatRef
          .current?.()
          .then((holdsLocks) => {
            if (holdsLocks !== false || document.visibilityState === "visible") return;
            pauseWatches({notifyServer: true});
          })
          .catch(() => {});
      }, IDLE_UNWATCH_MS);

      idleStreamTimer = window.setTimeout(() => {
        idleStreamTimer = null;
        idleStopped = true;
        clearRetryTimer();
        // Dropping the stream is how the locks and the presence go: the server releases everything the
        // connection held. Nothing to unwatch by hand, so the pause is local only.
        pauseWatches({notifyServer: false});
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
      if (connectionIdRef.current) flushWatches();
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
  }, [applyPresence, flushWatches, pauseWatches]);

  // The stream alone does not prove the tab is alive: a proxy between the browser and the server keeps
  // accepting writes for a tab that is gone. Without this the server never releases its locks or presence.
  useEffect(() => {
    if (!connectionId) return;

    let disposed = false;
    let applied = 0;

    const beat = async (): Promise<boolean | null> => {
      const at = Date.now();
      const {data, error} = await realtimeHeartbeat({body: {connectionId}});
      if (disposed || error !== undefined || data === undefined) return null;
      const holdsLocks = data.locks.length > 0;
      // The visibility beat and the interval one overlap; an overtaken reply would put a lock that was
      // already taken over back into the set, and the takeover would go unnoticed for another cycle.
      if (at <= applied) return holdsLocks;

      applied = at;
      setHeldLocks({keys: new Set(data.locks.map((l) => watchKey(l.entityType, l.entityId))), at});
      return holdsLocks;
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
      // A new connection holds nothing yet, and the old list must not outlive its connection.
      setHeldLocks(NO_LOCKS);
    };
  }, [connectionId]);

  const value = useMemo<RealtimeContextValue>(
    () => ({
      connectionId,
      isConnected: connectionId !== null,
      subscribe,
      watch,
      isWatching,
      presence,
      presenceKey: watchKey,
      heldLocks,
    }),
    [connectionId, subscribe, watch, isWatching, presence, heldLocks],
  );

  return <RealtimeContext.Provider value={value}>{children}</RealtimeContext.Provider>;
}
