import {useCallback, useEffect} from "react";
import {useSearchParamsContext} from "@/contexts/SearchParams/SearchParamsContext";
import {useSyncedWithQueryState} from "@/hooks/useSyncedWithQueryState";

// The query param is the source of truth; localStorage only supplies the value the page opens
// with when the URL carries no param, so a shared link still wins over the stored preference.
export function useSyncedWithQueryAndStorageState<T>(
  key: string,
  storageKey: string,
  fromQuery: (q: string | null) => T,
  toQuery: (v: T) => string,
): [T, (v: T) => void] {
  const {searchParams} = useSearchParamsContext();
  const [value, setValue] = useSyncedWithQueryState<T>(key, fromQuery, toQuery);
  const hasParam = searchParams.get(key) !== null;

  // toQuery is intentionally omitted from deps — a pure transformation whose reference changes
  // on every render but semantics don't.
  const setAndStore = useCallback(
    (v: T) => {
      localStorage.setItem(storageKey, toQuery(v));
      setValue(v);
    },
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [storageKey, setValue],
  );

  // Mount-only: from here on the param exists and the URL stays in charge.
  useEffect(() => {
    if (hasParam) return;
    const stored = localStorage.getItem(storageKey);
    if (stored !== null && stored !== toQuery(value)) setValue(fromQuery(stored));
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  return [value, setAndStore];
}
