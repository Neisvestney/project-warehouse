import type {ChipProps} from "@mui/material";
import type {OrderStatus, OrderType} from "@/api/types.gen";

export const ORDER_STATUS_LABELS: Record<OrderStatus, string> = {
  draft: "Черновик",
  confirmed: "Подтверждён",
  assembly: "На сборке",
  assembled: "Собран",
  shipped: "Отгружен",
  canceled: "Отменён",
};

export const ORDER_TYPE_LABELS: Record<OrderType, string> = {
  direct: "Прямой",
  fbs: "FBS",
  fbo: "FBO",
};

export const ORDER_STATUS_COLORS: Record<OrderStatus, ChipProps["color"]> = {
  draft: "default",
  confirmed: "info",
  assembly: "warning",
  assembled: "success",
  shipped: "primary",
  canceled: "error",
};

export const ORDER_TYPE_COLORS: Record<OrderType, ChipProps["color"]> = {
  direct: "default",
  fbs: "secondary",
  fbo: "primary",
};

export function formatOrderNumber(n: number): string {
  return `ЗКЗ-${String(n).padStart(5, "0")}`;
}

/**
 * Human-readable box name. Falls back to a 1-based position within
 * `orderBoxes` (the order's full box list) instead of exposing the box GUID,
 * so unlabeled boxes stay distinguishable and consistent across all
 * box-selection UIs.
 */
export function formatBoxLabel(
  box: {id: string; label?: string | null},
  orderBoxes: ReadonlyArray<{id: string}>,
): string {
  if (box.label) return box.label;
  const index = orderBoxes.findIndex((b) => b.id === box.id);
  return index === -1 ? "Коробка без названия" : `Коробка ${index + 1}`;
}
