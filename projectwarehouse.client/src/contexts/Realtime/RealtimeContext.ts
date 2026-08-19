import {createContext} from "react";
import type {AppEntityType, RealtimeEvent, RealtimeEventType} from "@/api/types.gen";

export interface RealtimeContextValue {
  connectionId: string | null;
  isConnected: boolean;
  subscribe: (type: RealtimeEventType, handler: (event: RealtimeEvent) => void) => () => void;
  /** Registers interest in an object; `onWatched` fires after every confirmed subscription. */
  watch: (entityType: AppEntityType, entityId: string, onWatched: () => void) => () => void;
  isWatching: (entityType: AppEntityType, entityId: string) => boolean;
}

const RealtimeContext = createContext<RealtimeContextValue | null>(null);
export default RealtimeContext;
