import {createContext} from "react";
import type {
  AppEntityType,
  RealtimeEvent,
  RealtimeEventType,
  RealtimeViewer,
} from "@/api/types.gen";

export interface RealtimeContextValue {
  connectionId: string | null;
  isConnected: boolean;
  subscribe: (type: RealtimeEventType, handler: (event: RealtimeEvent) => void) => () => void;
  /** Registers interest in an object; `onWatched` fires after every confirmed subscription. */
  watch: (entityType: AppEntityType, entityId: string, onWatched: () => void) => () => void;
  isWatching: (entityType: AppEntityType, entityId: string) => boolean;
  /** Viewers of every watched object, keyed by `entityType:entityId`. Includes the current user. */
  presence: ReadonlyMap<string, readonly RealtimeViewer[]>;
  presenceKey: (entityType: AppEntityType, entityId: string) => string;
}

const RealtimeContext = createContext<RealtimeContextValue | null>(null);
export default RealtimeContext;
