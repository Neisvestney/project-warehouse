import type {ReactNode} from "react";
import {CatalogItemDrawer} from "@/components/catalog/CatalogItemDrawer";
import {OpenCatalogItemContext} from "@/components/catalog/CatalogItemDrawerContext";
import {useDrawerSearchParamsState} from "@/hooks/useDrawerSearchParamsState";

// One drawer per page: components rendered in loops can't own it, or a click opens N of them.
export function CatalogItemDrawerHost({children}: {children: ReactNode}) {
  const [openedItemId, openDrawer, closeDrawer] = useDrawerSearchParamsState("catalogItem");

  return (
    <OpenCatalogItemContext.Provider value={openDrawer}>
      {children}
      <CatalogItemDrawer itemId={openedItemId} onClose={closeDrawer} onOpenItem={openDrawer} />
    </OpenCatalogItemContext.Provider>
  );
}

export default CatalogItemDrawerHost;
