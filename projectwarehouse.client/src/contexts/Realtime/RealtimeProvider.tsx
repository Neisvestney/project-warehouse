import {type ReactNode, useCallback, useMemo, useRef, useState} from "react";
import type {
  RealtimeEvent,
  RealtimeEventPayloadEntityPresenceChangedPayload,
  RealtimeEventType,
} from "@/api/types.gen";
import RealtimeContext, {type RealtimeContextValue} from "@/contexts/Realtime/RealtimeContext";
import {useHeartbeat} from "@/contexts/Realtime/useHeartbeat";
import {useRealtimeStream} from "@/contexts/Realtime/useRealtimeStream";
import {useWatchRegistry} from "@/contexts/Realtime/useWatchRegistry";
import {watchKey} from "@/contexts/Realtime/watchKey";

type EventHandler = (event: RealtimeEvent) => void;

/**
 * Wires the three machines the stream is made of — the connection itself, the registry of watched
 * objects, and the heartbeat that keeps locks alive — and hands their state to the app. The connection
 * id lives here because all three need it: the stream produces it, the other two spend it.
 */
export function RealtimeProvider({children}: {children: ReactNode}) {
  const [connectionId, setConnectionId] = useState<string | null>(null);
  const connectionIdRef = useRef<string | null>(null);

  const handlersRef = useRef(new Map<RealtimeEventType, Set<EventHandler>>());

  const registry = useWatchRegistry(connectionIdRef);
  const beat = useHeartbeat(connectionId);

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

  const {applyPresence, flush, reset, pause} = registry;

  const handleEvent = useCallback(
    (event: RealtimeEvent) => {
      if (event.type === "entityPresenceChanged") {
        applyPresence(event.payload as RealtimeEventPayloadEntityPresenceChangedPayload);
      }

      const handlers = handlersRef.current.get(event.type);
      if (!handlers) return;
      for (const handler of [...handlers]) {
        try {
          handler(event);
        } catch (e) {
          console.error("Realtime handler failed", e);
        }
      }
    },
    [applyPresence],
  );

  const handleConnected = useCallback(
    (id: string) => {
      // The ref is written before the state: `flush` runs in this same tick, and a re-render is a
      // tick away at best.
      connectionIdRef.current = id;
      setConnectionId(id);
      flush();
    },
    [flush],
  );

  const handleDisconnected = useCallback(() => {
    connectionIdRef.current = null;
    setConnectionId(null);
    reset();
  }, [reset]);

  const handleResumed = useCallback(() => {
    // A stream that survived the background needs its subscriptions back by hand; one that did not
    // gets them from `handleConnected` after it reconnects.
    if (connectionIdRef.current) flush();
  }, [flush]);

  useRealtimeStream({
    onEvent: handleEvent,
    onConnected: handleConnected,
    onDisconnected: handleDisconnected,
    onIdle: pause,
    onResumed: handleResumed,
    holdsLocks: beat,
  });

  const value = useMemo<RealtimeContextValue>(
    () => ({
      connectionId,
      isConnected: connectionId !== null,
      subscribe,
      watch: registry.watch,
      isWatching: registry.isWatching,
      presence: registry.presence,
      presenceKey: watchKey,
    }),
    [connectionId, subscribe, registry.watch, registry.isWatching, registry.presence],
  );

  return <RealtimeContext.Provider value={value}>{children}</RealtimeContext.Provider>;
}
