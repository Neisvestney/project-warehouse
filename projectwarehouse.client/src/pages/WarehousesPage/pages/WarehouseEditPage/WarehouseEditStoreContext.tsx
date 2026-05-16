import {createContext, useContext} from "react";
import type React from "react";
import type {WarehouseEditStore} from "./warehouseEditStore";

const WarehouseEditStoreContext = createContext<WarehouseEditStore | null>(null);

export function WarehouseEditStoreProvider({
  store,
  children,
}: {
  store: WarehouseEditStore;
  children: React.ReactNode;
}) {
  return (
    <WarehouseEditStoreContext.Provider value={store}>
      {children}
    </WarehouseEditStoreContext.Provider>
  );
}

// eslint-disable-next-line react-refresh/only-export-components
export function useWarehouseEditStore(): WarehouseEditStore {
  const store = useContext(WarehouseEditStoreContext);
  if (!store)
    throw new Error("useWarehouseEditStore must be used inside WarehouseEditStoreProvider");
  return store;
}
