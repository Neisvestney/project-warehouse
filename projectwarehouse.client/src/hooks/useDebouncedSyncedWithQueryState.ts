import {useDebouncedLocalState} from "@/hooks/useDebouncedLocalState";
import {useSyncedWithQueryState} from "@/hooks/useSyncedWithQueryState";

export function useDebouncedSyncedWithQueryState<
  T extends string | number | boolean | null | undefined,
>(
  key: string,
  fromQuery: (q: string | null) => T,
  toQuery: (v: T) => string | null | undefined,
  delay = 300,
): [T, (v: T) => void, T] {
  const [urlValue, setUrlValue] = useSyncedWithQueryState(key, fromQuery, toQuery);
  const [localValue, setLocalValue] = useDebouncedLocalState(urlValue, setUrlValue, delay);
  return [localValue, setLocalValue, urlValue];
}
