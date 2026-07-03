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
