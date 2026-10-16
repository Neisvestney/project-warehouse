import {createContext, useContext} from "react";
import type React from "react";
import type {RolesStore} from "./rolesStore";

interface RolesStoreContextValue {
  store: RolesStore;
  canEdit: boolean;
}

const RolesStoreContext = createContext<RolesStoreContextValue | null>(null);

export function RolesStoreProvider({
  store,
  canEdit,
  children,
}: {
  store: RolesStore;
  canEdit: boolean;
  children: React.ReactNode;
}) {
  return (
    <RolesStoreContext.Provider value={{store, canEdit}}>{children}</RolesStoreContext.Provider>
  );
}

// eslint-disable-next-line react-refresh/only-export-components
export function useRolesStore(): RolesStoreContextValue {
  const ctx = useContext(RolesStoreContext);
  if (!ctx) throw new Error("useRolesStore must be used inside RolesStoreProvider");
  return ctx;
}
