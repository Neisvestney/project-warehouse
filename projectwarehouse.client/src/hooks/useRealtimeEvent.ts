import {useEffect, useLayoutEffect, useRef} from "react";
import type {
  RealtimeEvent,
  RealtimeEventPayloadConnectionReadyPayload,
  RealtimeEventPayloadEditLockAcquiredPayload,
  RealtimeEventPayloadEditLockReleasedPayload,
  RealtimeEventPayloadEntityChangedPayload,
  RealtimeEventPayloadEntityPresenceChangedPayload,
  RealtimeEventPayloadMarketplaceSyncFinishedPayload,
  RealtimeEventPayloadMarketplaceSyncProgressPayload,
  RealtimeEventType,
} from "@/api/types.gen";
import {useRealtime} from "@/hooks/useRealtime";

// The generator emits the payload discriminator as optional, so narrowing by `payload.type`
// doesn't work — the envelope `type` is the reliable discriminator and this map follows it.
export type RealtimeEventPayloadFor<T extends RealtimeEventType> = T extends "connectionReady"
  ? RealtimeEventPayloadConnectionReadyPayload
  : T extends "marketplaceSyncProgress"
    ? RealtimeEventPayloadMarketplaceSyncProgressPayload
    : T extends "marketplaceSyncFinished"
      ? RealtimeEventPayloadMarketplaceSyncFinishedPayload
      : T extends "entityChanged"
        ? RealtimeEventPayloadEntityChangedPayload
        : T extends "editLockAcquired"
          ? RealtimeEventPayloadEditLockAcquiredPayload
          : T extends "editLockReleased"
            ? RealtimeEventPayloadEditLockReleasedPayload
            : T extends "entityPresenceChanged"
              ? RealtimeEventPayloadEntityPresenceChangedPayload
              : never;

export function useRealtimeEvent<T extends RealtimeEventType>(
  type: T,
  handler: (event: RealtimeEvent, payload: RealtimeEventPayloadFor<T>) => void,
): void {
  const {subscribe} = useRealtime();
  const handlerRef = useRef(handler);

  useLayoutEffect(() => {
    handlerRef.current = handler;
  });

  useEffect(
    () =>
      subscribe(type, (event) => {
        handlerRef.current(event, event.payload as RealtimeEventPayloadFor<T>);
      }),
    [subscribe, type],
  );
}
