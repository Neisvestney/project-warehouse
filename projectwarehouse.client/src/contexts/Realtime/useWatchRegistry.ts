import {type RefObject, useCallback, useRef, useState} from "react";
import {realtimeUnwatch, realtimeWatch} from "@/api/sdk.gen";
import type {
  AppEntityType,
  RealtimeEventPayloadEntityPresenceChangedPayload,
  RealtimeViewer,
} from "@/api/types.gen";
import type {RealtimeContextValue} from "@/contexts/Realtime/RealtimeContext";
import {watchKey} from "@/contexts/Realtime/watchKey";

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

export interface WatchRegistry {
  watch: RealtimeContextValue["watch"];
  isWatching: RealtimeContextValue["isWatching"];
  presence: ReadonlyMap<string, readonly RealtimeViewer[]>;
  applyPresence: (payload: RealtimeEventPayloadEntityPresenceChangedPayload) => void;
  /** Registers every entry under the current connection, lifting a pause if one is in effect. */
  flush: () => void;
  /** The connection is gone and the server forgot the subscriptions — only local state to clear. */
  reset: () => void;
  /** The connection is alive but the tab went idle: the server has to be told, and told to stay quiet. */
  pause: () => void;
}

/**
 * Which objects this tab is subscribed to, and the presence that comes with them. Reads the connection
 * through a ref rather than a value: the stream hands out a new id and flushes in the same tick, long
 * before React re-renders with it.
 */
export function useWatchRegistry(connectionIdRef: RefObject<string | null>): WatchRegistry {
  const [confirmedKeys, setConfirmedKeys] = useState<ReadonlySet<string>>(() => new Set());
  const [presence, setPresence] = useState<ReadonlyMap<string, readonly RealtimeViewer[]>>(
    () => new Map(),
  );

  const entriesRef = useRef(new Map<string, WatchEntry>());
  const pausedRef = useRef(false);

  const sendWatch = useCallback(
    async (entries: WatchEntry[]) => {
      if (pausedRef.current) return;
      const connection = connectionIdRef.current;
      // An entry unregistered within the same batch is dropped here: sending it would race its unwatch.
      const pending = entries.filter(
        (e) =>
          entriesRef.current.get(watchKey(e.entityType, e.entityId)) === e &&
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
        // 422 means the connection already died; a fresh one re-sends the whole batch. Entities the
        // user may not view are simply missing from `watched`, and those pages keep their polling.
        if (error !== undefined || data === undefined) return;
        if (connectionIdRef.current !== connection || pausedRef.current) return;

        const confirmed: string[] = [];
        for (const {entityType, entityId} of data.watched) {
          const key = watchKey(entityType, entityId);
          const entry = entriesRef.current.get(key);
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
            if (entriesRef.current.has(key)) next.set(key, viewers);
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
    },
    [connectionIdRef],
  );

  const flush = useCallback(() => {
    pausedRef.current = false;
    void sendWatch([...entriesRef.current.values()]);
  }, [sendWatch]);

  const reset = useCallback(() => {
    for (const entry of entriesRef.current.values()) {
      entry.confirmed = false;
      entry.pendingFor = null;
    }
    setConfirmedKeys(new Set());
    setPresence(new Map());
  }, []);

  // The entries themselves survive — the pages behind the tab are still mounted, so coming back
  // re-registers everything down the same path a reconnect takes.
  const pause = useCallback(() => {
    if (pausedRef.current) return;
    pausedRef.current = true;

    const entities = [...entriesRef.current.values()].map(({entityType, entityId}) => ({
      entityType,
      entityId,
    }));
    reset();

    const connection = connectionIdRef.current;
    if (!connection || entities.length === 0) return;
    void realtimeUnwatch({body: {connectionId: connection, entities}}).catch(() => {});
  }, [connectionIdRef, reset]);

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
            (e) => !entriesRef.current.has(watchKey(e.entityType, e.entityId)),
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
    [connectionIdRef, sendWatch],
  );

  const watch = useCallback<RealtimeContextValue["watch"]>(
    (entityType, entityId, onWatched) => {
      const key = watchKey(entityType, entityId);
      let entry = entriesRef.current.get(key);
      if (!entry) {
        entry = {
          entityType,
          entityId,
          refCount: 0,
          confirmed: false,
          pendingFor: null,
          callbacks: new Set(),
        };
        entriesRef.current.set(key, entry);
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
        const current = entriesRef.current.get(key);
        if (current !== registered) return;

        current.callbacks.delete(onWatched);
        current.refCount--;
        if (current.refCount > 0) return;

        entriesRef.current.delete(key);
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
    [connectionIdRef, enqueue],
  );

  const isWatching = useCallback<RealtimeContextValue["isWatching"]>(
    (entityType, entityId) => confirmedKeys.has(watchKey(entityType, entityId)),
    [confirmedKeys],
  );

  const applyPresence = useCallback((payload: RealtimeEventPayloadEntityPresenceChangedPayload) => {
    const key = watchKey(payload.entityType, payload.entityId);
    setPresence((prev) => {
      // An event for an object nobody on this tab watches any more would leak the entry back in.
      if (!entriesRef.current.has(key)) return prev;
      const next = new Map(prev);
      next.set(key, payload.viewers);
      return next;
    });
  }, []);

  return {watch, isWatching, presence, applyPresence, flush, reset, pause};
}
