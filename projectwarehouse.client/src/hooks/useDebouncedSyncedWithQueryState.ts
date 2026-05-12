import {useEffect, useRef, useState} from "react";
import {useDebounce} from "@/hooks/useDebounce";
import {useSyncedWithQueryState} from "@/hooks/useSyncedWithQueryState";

export function useDebouncedSyncedWithQueryState<T>(
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
  const prevUrlValue = useRef(urlValue);
  useEffect(() => {
    if (prevUrlValue.current !== urlValue) {
      prevUrlValue.current = urlValue;
      setLocalValue(urlValue);
    }
  }, [urlValue]);

  const isFirst = useRef(true);
  useEffect(() => {
    if (isFirst.current) {
      isFirst.current = false;
      return;
    }
    setUrlValue(debouncedLocal);
  }, [debouncedLocal, setUrlValue]);

  return [localValue, setLocalValue, urlValue];
}
