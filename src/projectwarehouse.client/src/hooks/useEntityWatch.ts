import {useCallback, useEffect, useLayoutEffect, useMemo, useRef} from "react";
import type {AppEntityType} from "@/api/types.gen";
import {useRealtime} from "@/hooks/useRealtime";

export interface EntityWatchResult {
  /** All requested subscriptions are registered on the server — the polling fallback can stop. */
  isWatching: boolean;
}

/**
 * Subscribes the stream to several objects for the lifetime of the component. `onWatched` fires
 * after every confirmed subscription, including re-subscription after a reconnect: reading right
 * after the watch is what closes the window where events could be missed.
 */
export function useEntityWatchMany(
  entityType: AppEntityType,
  entityIds: readonly string[],
  onWatched?: (entityId: string) => void,
): EntityWatchResult {
  const {watch, isWatching} = useRealtime();

  const onWatchedRef = useRef(onWatched);
  useLayoutEffect(() => {
    onWatchedRef.current = onWatched;
  });

  // A fresh array literal on every render would restart the watch/unwatch cycle endlessly.
  const idsKey = [...new Set(entityIds)].sort().join(",");
  const ids = useMemo(() => (idsKey ? idsKey.split(",") : []), [idsKey]);

  useEffect(() => {
    if (ids.length === 0) return;
    const disposers = ids.map((id) => watch(entityType, id, () => onWatchedRef.current?.(id)));
    return () => disposers.forEach((dispose) => dispose());
  }, [entityType, ids, watch]);

  return {isWatching: ids.length > 0 && ids.every((id) => isWatching(entityType, id))};
}

export function useEntityWatch(
  entityType: AppEntityType,
  entityId: string | null | undefined,
  onWatched?: () => void,
): EntityWatchResult {
  const onWatchedRef = useRef(onWatched);
  useLayoutEffect(() => {
    onWatchedRef.current = onWatched;
  });

  const handleWatched = useCallback(() => onWatchedRef.current?.(), []);
  return useEntityWatchMany(entityType, entityId ? [entityId] : [], handleWatched);
}
