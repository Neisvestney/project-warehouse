import {createContext, useContext} from "react";

export const OpenCatalogItemContext = createContext<((id: string) => void) | null>(null);

export function useOpenCatalogItem() {
  const open = useContext(OpenCatalogItemContext);
  if (!open) throw new Error("useOpenCatalogItem must be used inside CatalogItemDrawerHost");
  return open;
}
