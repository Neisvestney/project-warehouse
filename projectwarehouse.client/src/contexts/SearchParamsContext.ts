import {createContext, useContext} from "react";

export interface SearchParamsContextValue {
  searchParams: URLSearchParams;
  setParam: (key: string, value: string | null) => void;
}

export const SearchParamsContext = createContext<SearchParamsContextValue | null>(null);

export function useSearchParamsContext() {
  const ctx = useContext(SearchParamsContext);
  if (!ctx) throw new Error("useSearchParamsContext must be used within SearchParamsProvider");
  return ctx;
}
