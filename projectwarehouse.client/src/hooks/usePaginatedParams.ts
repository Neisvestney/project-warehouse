import {type DependencyList, useCallback, useEffect, useRef, useState} from "react";
import {useSyncedWithQueryState} from "@/hooks/useSyncedWithQueryState";

interface PaginatedParamsOptions {
  defaultPageSize?: number;
  debounceDelay?: number;
  pageParam?: string;
  pageSizeParam?: string;
}

export function usePaginatedParams<D extends object, I extends object = object>(
  debouncedParams: D,
  debouncedDeps: DependencyList,
  immediateParams?: I,
  immediateDeps?: DependencyList,
  options?: PaginatedParamsOptions,
): {
  fetchParams: D & I & {page: number; pageSize: number};
  page: number;
  setPage: (v: number) => void;
  pageSize: number;
  setPageSize: (v: number) => void;
} {
  const {
    defaultPageSize = 20,
    debounceDelay = 300,
    pageParam = "page",
    pageSizeParam = "pageSize",
  } = options ?? {};

  const [urlPage, setUrlPage] = useSyncedWithQueryState(
    pageParam,
    (q) => {
      const n = Number(q);
      return Number.isInteger(n) && n >= 1 ? n : 1;
    },
    (v) => (v === 1 ? null : String(v)),
  );

  const [pageSize, setPageSize] = useSyncedWithQueryState(
    pageSizeParam,
    (q) => {
      const n = Number(q);
      return Number.isInteger(n) && n >= 1 ? n : defaultPageSize;
    },
    (v) => (v === defaultPageSize ? null : String(v)),
  );

  // Debounced params and page are stored together so they update atomically.
  // This prevents fetchParams from ever having new search params + old page (or vice versa),
  // which would cause a spurious API call on every debounce settle while on page > 1.
  // Initial page is read from URL so direct links (/users?page=3) work correctly.
  const [settled, setSettled] = useState({params: debouncedParams, page: urlPage});

  // Sync settled.page when urlPage changes externally (browser back/forward, deep link).
  const prevUrlPage = useRef(urlPage);
  useEffect(() => {
    if (prevUrlPage.current !== urlPage) {
      prevUrlPage.current = urlPage;

      setSettled((prev) => ({...prev, page: urlPage}));
    }
  }, [urlPage]);

  const isFirstDebounce = useRef(true);
  useEffect(() => {
    if (isFirstDebounce.current) {
      isFirstDebounce.current = false;
      return;
    }
    // setState inside a setTimeout callback is async — not a sync effect body, linter-compliant
    const timer = setTimeout(() => {
      setSettled({params: debouncedParams, page: 1});
      setUrlPage(1);
    }, debounceDelay);
    return () => clearTimeout(timer);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, debouncedDeps);

  const isFirstImmediate = useRef(true);
  useEffect(() => {
    if (isFirstImmediate.current) {
      isFirstImmediate.current = false;
      return;
    }
    // setTimeout(fn, 0) keeps setState out of the synchronous effect body
    const timer = setTimeout(() => {
      setSettled((prev) => ({...prev, page: 1}));
      setUrlPage(1);
    }, 0);
    return () => clearTimeout(timer);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, immediateDeps ?? []);

  const setPage = useCallback(
    (newPage: number) => {
      setSettled((prev) => ({...prev, page: newPage}));
      setUrlPage(newPage);
    },

    [setUrlPage],
  );

  const fetchParams = {
    ...settled.params,
    ...(immediateParams as object),
    page: settled.page,
    pageSize,
  } as D & I & {page: number; pageSize: number};

  return {fetchParams, page: settled.page, setPage, pageSize, setPageSize};
}
