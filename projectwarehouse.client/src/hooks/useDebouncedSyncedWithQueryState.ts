import {useEffect, useRef, useState} from "react";
import {useDebounce} from "@/hooks/useDebounce";
import {useSyncedWithQueryState} from "@/hooks/useSyncedWithQueryState";

// T is constrained to primitives: a `fromQuery` returning a fresh object every render would make the
// URL-change check below fire forever.
export function useDebouncedSyncedWithQueryState<
  T extends string | number | boolean | null | undefined,
>(
  key: string,
  fromQuery: (q: string | null) => T,
  toQuery: (v: T) => string | null | undefined,
  delay = 300,
): [T, (v: T) => void, T] {
  const [urlValue, setUrlValue] = useSyncedWithQueryState(key, fromQuery, toQuery);

  // Local state for lag-free input; debounce then sync to URL.
  const [localValue, setLocalValue] = useState(urlValue);
  const debouncedLocal = useDebounce(localValue, delay);

  // Sync local state when URL changes externally (browser back/forward, deep link).
  // Our own pushes echo back here too, and react-router commits them inside a transition,
  // so the echo can land after the user typed more — ignoring it prevents losing characters.
  const prevUrlValue = useRef(urlValue);
  const lastPushed = useRef<T | undefined>(undefined);
  useEffect(() => {
    if (prevUrlValue.current !== urlValue) {
      prevUrlValue.current = urlValue;
      if (!Object.is(urlValue, lastPushed.current)) setLocalValue(urlValue);
    }
  }, [urlValue]);

  const isFirst = useRef(true);
  useEffect(() => {
    if (isFirst.current) {
      isFirst.current = false;
      return;
    }
    lastPushed.current = debouncedLocal;
    setUrlValue(debouncedLocal);
  }, [debouncedLocal, setUrlValue]);

  return [localValue, setLocalValue, urlValue];
}
