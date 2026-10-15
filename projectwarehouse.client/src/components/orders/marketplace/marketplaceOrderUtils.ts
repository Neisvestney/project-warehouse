import type {ChipProps} from "@mui/material";
import type {
  MarketplaceCancellationType,
  MarketplaceLossReason,
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
  awaitingPackaging: "Ожидает сборки",
  awaitingDeliver: "Ожидает отгрузки",
  delivering: "В доставке",
  delivered: "Доставлен",
  cancelled: "Отменён",
  arbitration: "Арбитраж",
  split: "Разделён",
};

export const ALL_MARKETPLACE_ORDER_STATUSES: MarketplaceOrderStatus[] = [
  "awaitingPackaging",
  "awaitingDeliver",
  "delivering",
  "delivered",
  "cancelled",
  "arbitration",
  "split",
  "unknown",
];

export const MARKETPLACE_ORDER_STATUS_COLORS: Record<MarketplaceOrderStatus, ChipProps["color"]> = {
  unknown: "default",
  awaitingPackaging: "default",
  awaitingDeliver: "info",
  delivering: "primary",
  delivered: "success",
  cancelled: "error",
  arbitration: "warning",
  split: "default",
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

export type MarketplaceLossReasonGroup = "buyer" | "seller" | "marketplace" | "unknown";

export const MARKETPLACE_LOSS_REASON_GROUP_LABELS: Record<MarketplaceLossReasonGroup, string> = {
  buyer: "Покупатель",
  seller: "Продавец",
  marketplace: "Площадка и доставка",
  unknown: "Не распознана",
};

export const MARKETPLACE_LOSS_REASON_GROUPS: Record<
  MarketplaceLossReason,
  MarketplaceLossReasonGroup
> = {
  unknown: "unknown",
  buyerChangedMind: "buyer",
  buyerFoundCheaper: "buyer",
  buyerDeliveryTerms: "buyer",
  buyerDidNotFit: "buyer",
  buyerNotPickedUp: "buyer",
  buyerOther: "buyer",
  outOfStock: "seller",
  notShippedInTime: "seller",
  sellerCancelled: "seller",
  defect: "seller",
  poorQuality: "seller",
  wrongItem: "seller",
  incomplete: "seller",
  cardMismatch: "seller",
  counterfeit: "seller",
  deliveryFailed: "marketplace",
  deliveryDelayed: "marketplace",
  damagedInDelivery: "marketplace",
  marketplaceFailure: "marketplace",
  marketplaceOther: "marketplace",
};

export const MARKETPLACE_LOSS_REASON_LABELS: Record<MarketplaceLossReason, string> = {
  unknown: "Причина не распознана",
  buyerChangedMind: "Передумал",
  buyerFoundCheaper: "Нашёл дешевле",
  buyerDeliveryTerms: "Не устроил срок доставки",
  buyerDidNotFit: "Не подошёл",
  buyerNotPickedUp: "Не забрал заказ",
  buyerOther: "Другое",
  outOfStock: "Нет в наличии",
  notShippedInTime: "Не отгружен вовремя",
  sellerCancelled: "Отменён продавцом",
  defect: "Брак",
  poorQuality: "Недоволен качеством",
  wrongItem: "Не тот товар",
  incomplete: "Неполная комплектация",
  cardMismatch: "Не соответствует карточке",
  counterfeit: "Подделка",
  deliveryFailed: "Не удалось доставить",
  deliveryDelayed: "Перенос сроков доставки",
  damagedInDelivery: "Повреждён при доставке",
  marketplaceFailure: "Сбой площадки",
  marketplaceOther: "Другое",
};

/** «Группа · причина» — the reason alone is ambiguous («Другое» exists in two groups). */
export function formatLossReason(reason: MarketplaceLossReason): string {
  if (reason === "unknown") return MARKETPLACE_LOSS_REASON_LABELS.unknown;
  const group = MARKETPLACE_LOSS_REASON_GROUP_LABELS[MARKETPLACE_LOSS_REASON_GROUPS[reason]];
  return `${group} · ${MARKETPLACE_LOSS_REASON_LABELS[reason]}`;
}

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
