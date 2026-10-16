import {createContext, useContext} from "react";

export interface GlobalSearchRequest {
  camera?: boolean;
  query?: string;
}

export interface GlobalSearchApi {
  isOpen: boolean;
  openSearch: (request?: GlobalSearchRequest) => void;
  closeSearch: () => void;
}

export const GlobalSearchContext = createContext<GlobalSearchApi | null>(null);

export function useGlobalSearch(): GlobalSearchApi {
  const api = useContext(GlobalSearchContext);
  if (!api) throw new Error("useGlobalSearch must be used inside GlobalSearchProvider");
  return api;
}
