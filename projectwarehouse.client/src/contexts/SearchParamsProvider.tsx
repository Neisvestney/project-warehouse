import {ReactNode, useCallback, useLayoutEffect, useRef} from "react";
import {useSearchParams} from "react-router";
import {SearchParamsContext} from "@/contexts/SearchParamsContext";

export function SearchParamsProvider({children}: {children: ReactNode}) {
  const [searchParams, setSearchParams] = useSearchParams();
  const pendingRef = useRef<Map<string, string | null>>(new Map());
  const scheduledRef = useRef(false);
  const setSearchParamsRef = useRef(setSearchParams);

  // Keep ref in sync with the latest setSearchParams from React Router
  useLayoutEffect(() => {
    setSearchParamsRef.current = setSearchParams;
  });

  const setParam = useCallback((key: string, value: string | null) => {
    pendingRef.current.set(key, value);
    if (!scheduledRef.current) {
      scheduledRef.current = true;
      // Collect all setParam calls in the same synchronous tick into one navigation
      queueMicrotask(() => {
        scheduledRef.current = false;
        const updates = new Map(pendingRef.current);
        pendingRef.current.clear();
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
