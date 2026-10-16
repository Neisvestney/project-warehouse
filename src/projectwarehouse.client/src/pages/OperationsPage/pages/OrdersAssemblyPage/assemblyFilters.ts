import type {OrderDetailsDto} from "@/api/types.gen";
import type {TagFilter} from "@/features/tags";
import {matchesSearchTokens, toSearchTokens} from "@/utils/searchUtils";

export interface AssemblyFilters {
  searchString: string;
  warehouseId: string | null;
  catalogItemIds: readonly string[];
  tagFilter: TagFilter;
}

/** The worklist arrives unfiltered; these are the same rules the server applies to the orders list. */
export function filterAssemblyOrders(
  orders: readonly OrderDetailsDto[],
  {searchString, warehouseId, catalogItemIds, tagFilter}: AssemblyFilters,
): OrderDetailsDto[] {
  const tokens = toSearchTokens(searchString);
  const itemIds = new Set(catalogItemIds);
  return orders.filter(
    (o) =>
      (!warehouseId || o.warehouseId === warehouseId) &&
      (itemIds.size === 0 ||
        o.boxes.some((b) => b.components.some((c) => itemIds.has(c.catalogItemId)))) &&
      (tagFilter.include.length === 0 || o.tags.some((t) => tagFilter.include.includes(t.id))) &&
      !o.tags.some((t) => tagFilter.exclude.includes(t.id)) &&
      matchesSearchTokens(o.extendedSearchString ?? "", tokens),
  );
}
