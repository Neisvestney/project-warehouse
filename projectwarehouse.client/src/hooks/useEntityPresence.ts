import {useMemo} from "react";
import type {AppEntityType, RealtimeViewer} from "@/api/types.gen";
import {useAuth} from "@/hooks/useAuth";
import {useRealtime} from "@/hooks/useRealtime";

/**
 * Other people looking at the object right now. Reads what the stream already knows — the page still
 * has to be subscribed (`useEntityWatch` or `useEditLock`) for anything to show up.
 */
export function useEntityPresence(
  entityType: AppEntityType,
  entityId: string | null | undefined,
): readonly RealtimeViewer[] {
  const {presence, presenceKey} = useRealtime();
  const {user} = useAuth();

  return useMemo(() => {
    if (!entityId) return [];

    const viewers = presence.get(presenceKey(entityType, entityId)) ?? [];
    // Yourself is not news; the server sends the full list so every tab sees the same thing.
    return viewers.filter((v) => v.userId !== user?.id);
  }, [presence, presenceKey, entityType, entityId, user?.id]);
}
