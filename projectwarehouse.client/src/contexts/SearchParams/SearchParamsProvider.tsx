import {type ReactNode, useCallback, useLayoutEffect, useRef} from "react";
import {useSearchParams} from "react-router";
import {SearchParamsContext} from "@/contexts/SearchParams/SearchParamsContext";

export function SearchParamsProvider({children}: {children: ReactNode}) {
  const [searchParams, setSearchParams] = useSearchParams();
  const pendingRef = useRef<Map<string, string | null>>(new Map());
  const scheduledRef = useRef(false);
  const setSearchParamsRef = useRef(setSearchParams);
  // Updates pushed but not yet reflected in the router's params. Navigations commit inside a
  // transition, so a later batch would otherwise build on a `prev` that still misses them.
  const unconfirmedRef = useRef<Map<string, string | null>>(new Map());

  // Keep ref in sync with the latest setSearchParams from React Router
  useLayoutEffect(() => {
    setSearchParamsRef.current = setSearchParams;

    for (const [k, v] of unconfirmedRef.current) {
      if (searchParams.get(k) === v) unconfirmedRef.current.delete(k);
    }
  });

  const setParam = useCallback((key: string, value: string | null) => {
    pendingRef.current.set(key, value);
    if (!scheduledRef.current) {
      scheduledRef.current = true;
      // Collect all setParam calls in the same synchronous tick into one navigation
      queueMicrotask(() => {
        scheduledRef.current = false;
        for (const [k, v] of pendingRef.current) {
          unconfirmedRef.current.set(k, v);
        }
        pendingRef.current.clear();
        // Snapshot: the updater runs at commit time, when the map has already moved on.
        const updates = new Map(unconfirmedRef.current);
        setSearchParamsRef.current(
          (prev) => {
            const next = new URLSearchParams(prev);
            for (const [k, v] of updates) {
              if (v == null) {
                next.delete(k);
              } else {
                next.set(k, v);
              }
            }
            return next;
          },
          {replace: true},
        );
      });
    }
  }, []);

  return (
    <SearchParamsContext.Provider value={{searchParams, setParam}}>
      {children}
    </SearchParamsContext.Provider>
  );
}
