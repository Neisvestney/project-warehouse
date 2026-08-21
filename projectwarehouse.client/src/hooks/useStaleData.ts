import {useCallback, useLayoutEffect, useRef, useState} from "react";
import type {AppEntityType} from "@/api/types.gen";
import {useAuth} from "@/hooks/useAuth";
import {useEntityWatch} from "@/hooks/useEntityWatch";
import {useRealtimeEvent} from "@/hooks/useRealtimeEvent";
import {useSilentRefresh} from "@/hooks/useSilentRefresh";

export interface StaleActor {
  userId: string;
  userName: string;
}

export interface UseStaleDataOptions {
  /** Form state from the page. An untouched form is refreshed silently instead of warning. */
  isDirty?: boolean;
  /** `dataUpdatedAt` of the object's query — a read newer than the event clears the flag by itself. */
  dataUpdatedAt?: number;
  /** `isFetching` of the object's query — tells apart a finished background refresh from a new one. */
  isFetching?: boolean;
  /** `isLoading` of the object's query — the first read renders its own placeholder, not an overlay. */
  isLoading?: boolean;
  /** Invalidates the object's queries. Also runs on every confirmed subscription. */
  onRefresh: () => void;
  enabled?: boolean;
}

export interface UseStaleDataResult {
  isStale: boolean;
  /** The running refetch was triggered by a confirmed subscription — pages keep the loader hidden. */
  isSilentRefresh: boolean;
  /** Ready-made `open` for `LoadingOverlay`: a visible refetch over already rendered data. */
  showLoadingOverlay: boolean;
  staleBy: StaleActor | null;
  isWatching: boolean;
  refresh: () => void;
  dismissStale: () => void;
}

/**
 * Warns that the shown object may have been saved by someone else. Split out of `useEditLock` on
 * purpose: a page can carry an editable form without being allowed to lock the object — an assembler
 * on an order is exactly that — and would otherwise get no warning at all.
 */
export function useStaleData(
  entityType: AppEntityType,
  entityId: string | null | undefined,
  {
    isDirty = false,
    dataUpdatedAt = 0,
    isFetching = false,
    isLoading = false,
    onRefresh,
    enabled = true,
  }: UseStaleDataOptions,
): UseStaleDataResult {
  const {user} = useAuth();
  const [staleAt, setStaleAt] = useState<number | null>(null);
  const [staleBy, setStaleBy] = useState<StaleActor | null>(null);

  const isDirtyRef = useRef(isDirty);
  const onRefreshRef = useRef(onRefresh);
  useLayoutEffect(() => {
    isDirtyRef.current = isDirty;
    onRefreshRef.current = onRefresh;
  });

  const {isSilentRefresh, markSilent, showLoadingOverlay} = useSilentRefresh(isFetching, isLoading);

  const watchedId = enabled ? entityId : null;
  // The subscription is confirmed right after mount, so its refetch trails the first read — no loader.
  const handleWatched = useCallback(() => {
    markSilent();
    onRefreshRef.current();
  }, [markSilent]);
  const {isWatching} = useEntityWatch(entityType, watchedId, handleWatched);

  const markStale = useCallback((by: StaleActor | null) => {
    // An untouched form has nothing to lose, so it is brought up to date without a banner.
    if (!isDirtyRef.current) {
      onRefreshRef.current();
      return;
    }
    setStaleBy(by);
    setStaleAt(Date.now());
  }, []);

  const matches = useCallback(
    (payloadType: AppEntityType, payloadId: string) =>
      enabled && !!entityId && payloadType === entityType && payloadId === entityId,
    [enabled, entityId, entityType],
  );

  useRealtimeEvent("entityChanged", (_event, payload) => {
    if (!matches(payload.entityType, payload.entityId)) return;
    if (payload.byUserId && payload.byUserId === user?.id) return;

    markStale(
      payload.byUserId && payload.byUserName
        ? {userId: payload.byUserId, userName: payload.byUserName}
        : null,
    );
  });

  // Fallback trigger: someone left the object, and their edit may have produced no changelog entry.
  useRealtimeEvent("editLockReleased", (_event, payload) => {
    if (!matches(payload.entityType, payload.entityId)) return;
    if (payload.userId === user?.id) return;

    markStale({userId: payload.userId, userName: payload.userName});
  });

  const dismissStale = useCallback(() => {
    setStaleAt(null);
    setStaleBy(null);
  }, []);

  const refresh = useCallback(() => {
    onRefreshRef.current();
    dismissStale();
  }, [dismissStale]);

  // TanStack refetches every active query on focus and `staleTime` is 0 app-wide, so a read that
  // landed after the event already answered the warning — no need to clear the flag by hand.
  const isStale = staleAt !== null && dataUpdatedAt < staleAt;

  return {
    isStale,
    staleBy: isStale ? staleBy : null,
    isSilentRefresh,
    showLoadingOverlay,
    isWatching,
    refresh,
    dismissStale,
  };
}
