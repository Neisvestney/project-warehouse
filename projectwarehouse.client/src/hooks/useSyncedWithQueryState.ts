import {useCallback, useMemo} from "react";
import {useSearchParamsContext} from "@/contexts/SearchParams/SearchParamsContext";

export function useSyncedWithQueryState<T>(
  key: string,
  fromQuery: (q: string | null) => T,
  toQuery: (v: T) => string | null | undefined,
): [T, (v: T) => void] {
  const {searchParams, setParam} = useSearchParamsContext();

  // fromQuery is intentionally omitted from deps — it's always a pure transformation
  // whose reference changes on every render but semantics don't.
  // eslint-disable-next-line react-hooks/exhaustive-deps
  const value = useMemo(() => fromQuery(searchParams.get(key)), [searchParams, key]);

  // toQuery is intentionally omitted from deps for the same reason, keeping setValue stable.
  const setValue = useCallback(
    (v: T) => setParam(key, toQuery(v) ?? null),
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [key, setParam],
  );

  return [value, setValue];
}
