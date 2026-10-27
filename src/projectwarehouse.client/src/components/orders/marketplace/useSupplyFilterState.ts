import {useSyncedWithQueryState} from "@/hooks/useSyncedWithQueryState";

/** Supply filter of the orders list, kept in the `supply` URL param so a table cell can set it too. */
export function useSupplyFilterState(): [string | null, (value: string | null) => void] {
  return useSyncedWithQueryState(
    "supply",
    (q) => (typeof q === "string" && q ? q : null),
    (v) => v,
  );
}
