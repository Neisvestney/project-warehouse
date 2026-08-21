import {useCallback, useEffect, useRef, useState} from "react";

/** How long to wait for a marked refetch to start; without it the flag could hang forever. */
const START_GRACE_MS = 1000;

export interface UseSilentRefreshResult {
  /** A refetch marked as background is running (or about to) — the page keeps its loader hidden. */
  isSilentRefresh: boolean;
  /** Call right before a refetch that must not show the loader. */
  markSilent: () => void;
  /** Ready-made `open` for `LoadingOverlay`: a visible refetch over already rendered data. */
  showLoadingOverlay: boolean;
}

/**
 * Marks individual refetches as background ones. `isFetching` comes from the object's query: the flag
 * holds while the marked request is in flight and clears on the first transition back to false.
 */
export function useSilentRefresh(isFetching = false, isLoading = false): UseSilentRefreshResult {
  const [isSilentRefresh, setIsSilentRefresh] = useState(false);
  const silentRef = useRef(false);
  const startedRef = useRef(false);

  // A second mark while the first request is still in flight keeps waiting for that request, so the
  // flag is not thrown back to "not started yet" and stretched by the grace period for nothing.
  const markSilent = useCallback(() => {
    if (!silentRef.current) startedRef.current = false;
    silentRef.current = true;
    setIsSilentRefresh(true);
  }, []);

  const clearSilent = useCallback(() => {
    silentRef.current = false;
    startedRef.current = false;
    setIsSilentRefresh(false);
  }, []);

  useEffect(() => {
    if (!isSilentRefresh) return;

    if (isFetching) {
      startedRef.current = true;
      return;
    }
    if (startedRef.current) {
      clearSilent();
      return;
    }

    const timer = setTimeout(clearSilent, START_GRACE_MS);
    return () => clearTimeout(timer);
  }, [isSilentRefresh, isFetching, clearSilent]);

  return {
    isSilentRefresh,
    markSilent,
    showLoadingOverlay: isFetching && !isLoading && !isSilentRefresh,
  };
}
