import {useMemo} from "react";
import {useSyncedWithQueryState} from "@/hooks/useSyncedWithQueryState";
import type {StockMovementsFilterValue} from "./useStockMovementsPivot";

function parseList(query: string | null): string[] {
  return query ? query.split(",").filter(Boolean) : [];
}

function serializeList(value: string[]): string | null {
  return value.length > 0 ? value.join(",") : null;
}

function parseId(query: string | null): string | null {
  return query || null;
}

const DATE_PATTERN = /^\d{4}-\d{2}-\d{2}$/;

function parseDate(query: string | null): string | null {
  return query && DATE_PATTERN.test(query) ? query : null;
}

export function useStockMovementsFilters() {
  const [catalogItemIds, setCatalogItemIds] = useSyncedWithQueryState(
    "items",
    parseList,
    serializeList,
  );
  const [from, setFrom] = useSyncedWithQueryState("from", parseDate, (v) => v);
  const [to, setTo] = useSyncedWithQueryState("to", parseDate, (v) => v);
  const [warehouseId, setWarehouseId] = useSyncedWithQueryState("warehouse", parseId, (v) => v);
  const [storagePlaceId, setStoragePlaceId] = useSyncedWithQueryState("place", parseId, (v) => v);
  const [nodeId, setNodeId] = useSyncedWithQueryState("node", parseId, (v) => v);
  const [userId, setUserId] = useSyncedWithQueryState("user", parseId, (v) => v);

  // Which preset is open and whether the table fills the tab are display state, not filters — they
  // stay out of `filter` so switching a preset does not invalidate the pivot query key.
  const [presetId, setPresetId] = useSyncedWithQueryState("preset", parseId, (v) => v);
  const [expanded, setExpanded] = useSyncedWithQueryState(
    "full",
    (q) => q === "1",
    (v) => (v ? "1" : null),
  );

  const filter = useMemo<StockMovementsFilterValue>(
    () => ({
      catalogItemIds,
      from,
      to,
      warehouseId,
      storagePlaceId,
      nodeId,
      userId,
    }),
    [catalogItemIds, from, to, warehouseId, storagePlaceId, nodeId, userId],
  );

  return {
    filter,
    presetId,
    setPresetId,
    expanded,
    setExpanded,
    setCatalogItemIds,
    setFrom,
    setTo,
    // A storage place belongs to a warehouse and a node to a place — keep the narrower ones from
    // outliving the scope that produced them.
    setWarehouseId: (value: string | null) => {
      setWarehouseId(value);
      setStoragePlaceId(null);
      setNodeId(null);
    },
    setStoragePlaceId: (value: string | null) => {
      setStoragePlaceId(value);
      setNodeId(null);
    },
    setNodeId,
    setUserId,
  };
}
