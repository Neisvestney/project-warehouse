import {createContext, useContext} from "react";
import type {SeasonId} from "@/features/seasonal/seasons";

export const SeasonalContext = createContext<readonly SeasonId[]>([]);

export function useActiveSeasons() {
  return useContext(SeasonalContext);
}
