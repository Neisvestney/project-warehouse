import {useCallback, useMemo} from "react";
import {useSearchParamsContext} from "@/contexts/SearchParams/SearchParamsContext";

export function useSyncedWithQueryState<T>(
  key: string,
  fromQuery: (q: string | null) => T,
  toQuery: (v: T) => string | null | undefined,
): [T, (v: T) => void] {
  const {searchParams, setParam} = useSearchParamsContext();

  const raw = searchParams.get(key);

  // Keyed on the raw string, not the searchParams object: its identity changes on every
  // navigation, which would hand out a fresh array/object to callers using `value` as an effect dep.
  // fromQuery is intentionally omitted from deps — it's always a pure transformation
  // whose reference changes on every render but semantics don't.
  // eslint-disable-next-line react-hooks/exhaustive-deps
  const value = useMemo(() => fromQuery(raw), [raw, key]);

  // toQuery is intentionally omitted from deps for the same reason, keeping setValue stable.
  const setValue = useCallback(
    (v: T) => setParam(key, toQuery(v) ?? null),
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [key, setParam],
  );

  return [value, setValue];
}
