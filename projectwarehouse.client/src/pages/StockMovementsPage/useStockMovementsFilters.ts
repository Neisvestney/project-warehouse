import {useMemo} from "react";
import type {StockMovementDirection} from "@/api/types.gen";
import {useSyncedWithQueryState} from "@/hooks/useSyncedWithQueryState";
import {STOCK_MOVEMENT_ACTIONS, STOCK_MOVEMENT_DIRECTIONS} from "./stockMovementsConstants";
import type {StockMovementsFilterValue} from "./useStockMovementsPivot";

const ACTION_VALUES = STOCK_MOVEMENT_ACTIONS.map((a) => a.value);
const DIRECTION_VALUES: string[] = STOCK_MOVEMENT_DIRECTIONS.map((d) => d.value);

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
  const [receiptTagIds, setReceiptTagIds] = useSyncedWithQueryState(
    "receiptTags",
    parseList,
    serializeList,
  );

  const [actions, setActions] = useSyncedWithQueryState(
    "actions",
    (q) => parseList(q).filter((a) => ACTION_VALUES.includes(a)),
    serializeList,
  );
  const [directions, setDirections] = useSyncedWithQueryState(
    "directions",
    (q) => parseList(q).filter((d): d is StockMovementDirection => DIRECTION_VALUES.includes(d)),
    serializeList,
  );

  const [showTransfers, setShowTransfers] = useSyncedWithQueryState(
    "transfers",
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
      receiptTagIds,
      actions,
      directions,
    }),
    [
      catalogItemIds,
      from,
      to,
      warehouseId,
      storagePlaceId,
      nodeId,
      userId,
      receiptTagIds,
      actions,
      directions,
    ],
  );

  return {
    filter,
    showTransfers,
    setShowTransfers,
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
    setReceiptTagIds,
    setActions,
    setDirections,
  };
}
