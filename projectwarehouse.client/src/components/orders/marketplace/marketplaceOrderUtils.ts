import type {ChipProps} from "@mui/material";
import type {MarketplaceOrderStatus, MarketplaceSyncStatus, MarketplaceType} from "@/api/types.gen";

// Живёт здесь, а не в дереве настроек: раздел операций не должен импортировать из настроек.
export const MARKETPLACE_ORDER_STATUS_LABELS: Record<MarketplaceOrderStatus, string> = {
  unknown: "Неизвестен",
  awaitingDeliver: "Ожидает отгрузки",
  delivering: "В доставке",
  delivered: "Доставлен",
  cancelled: "Отменён",
  arbitration: "Арбитраж",
};

export const ALL_MARKETPLACE_ORDER_STATUSES: MarketplaceOrderStatus[] = [
  "awaitingDeliver",
  "delivering",
  "delivered",
  "cancelled",
  "arbitration",
  "unknown",
];

export const MARKETPLACE_ORDER_STATUS_COLORS: Record<MarketplaceOrderStatus, ChipProps["color"]> = {
  unknown: "default",
  awaitingDeliver: "info",
  delivering: "primary",
  delivered: "success",
  cancelled: "error",
  arbitration: "warning",
};

export const MARKETPLACE_LABELS: Record<MarketplaceType, string> = {
  ozon: "Ozon",
  wildberries: "Wildberries",
};

export const ALL_MARKETPLACE_TYPES: MarketplaceType[] = ["ozon", "wildberries"];

export const RUN_STATUS_LABELS: Record<MarketplaceSyncStatus, string> = {
  running: "Выполняется",
  success: "Готово",
  failed: "Ошибка",
  canceled: "Отменена",
};

export const RUN_STATUS_COLORS: Record<MarketplaceSyncStatus, ChipProps["color"]> = {
  running: "info",
  success: "success",
  failed: "error",
  canceled: "default",
};
