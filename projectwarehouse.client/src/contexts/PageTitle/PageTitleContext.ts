import {createContext} from "react";

export interface PageTitleRegistry {
  /** Returns the unregister callback. */
  register: (id: string, depth: number, title: string) => () => void;
}

export const PageTitleRegistryContext = createContext<PageTitleRegistry | null>(null);

export const PageTitleDepthContext = createContext(0);
