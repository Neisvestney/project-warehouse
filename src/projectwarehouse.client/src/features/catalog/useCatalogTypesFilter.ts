import type {CatalogItemType} from "@/api/types.gen";
import {useSyncedWithQueryState} from "@/hooks/useSyncedWithQueryState";
import {CATALOG_ITEM_TYPES} from "./catalogItemTypes";

/**
 * An empty selection needs its own marker: serializing it to `null` would drop the param and the
 * next parse would read it back as "all types".
 */
export const NO_ITEM_TYPES = "none";

/** Multiselect type filter kept in a URL param; the full `options` set is the default and stays out of the URL. */
export function useCatalogTypesFilter(
  key = "types",
  options: CatalogItemType[] = CATALOG_ITEM_TYPES,
): [CatalogItemType[], (value: CatalogItemType[]) => void] {
  return useSyncedWithQueryState<CatalogItemType[]>(
    key,
    (q) => {
      if (!q) return options;
      if (q === NO_ITEM_TYPES) return [];
      const parsed = q.split(",").filter((p) => options.includes(p as CatalogItemType));
      return parsed.length > 0 ? (parsed as CatalogItemType[]) : options;
    },
    (v) => {
      const isDefault = v.length === options.length && options.every((t) => v.includes(t));
      if (isDefault) return null;
      return v.length > 0 ? v.join(",") : NO_ITEM_TYPES;
    },
  );
}
