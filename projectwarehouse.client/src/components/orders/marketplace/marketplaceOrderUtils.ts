import type {ChipProps} from "@mui/material";
import type {
  MarketplaceCancellationType,
  MarketplaceOrderReturnState,
  MarketplaceOrderStatus,
  MarketplaceReturnCompensationStatus,
  MarketplaceReturnKind,
  MarketplaceSyncStatus,
  MarketplaceType,
} from "@/api/types.gen";

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

export const MARKETPLACE_ORDER_RETURN_STATE_LABELS: Record<MarketplaceOrderReturnState, string> = {
  none: "Нет",
  partial: "Частичный возврат",
  full: "Полный возврат",
};

export const MARKETPLACE_ORDER_RETURN_STATE_COLORS: Record<
  MarketplaceOrderReturnState,
  ChipProps["color"]
> = {
  none: "default",
  partial: "warning",
  full: "error",
};

export const MARKETPLACE_RETURN_KIND_LABELS: Record<MarketplaceReturnKind, string> = {
  unknown: "Технический возврат",
  cancellation: "Отмена после отгрузки",
  fullRefusal: "Полный отказ при вручении",
  partialRefusal: "Частичный отказ при вручении",
  customerReturn: "Возврат после вручения",
};

export const MARKETPLACE_RETURN_COMPENSATION_LABELS: Record<
  MarketplaceReturnCompensationStatus,
  string
> = {
  sent: "Отправлена",
  received: "Получена",
  canceled: "Отменена",
  decompensationSent: "Отправлена декомпенсация",
};

export const MARKETPLACE_CANCELLATION_TYPE_LABELS: Record<MarketplaceCancellationType, string> = {
  unknown: "Неизвестен",
  seller: "Продавец",
  customer: "Покупатель",
  marketplace: "Площадка",
  system: "Система",
  delivery: "Служба доставки",
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
