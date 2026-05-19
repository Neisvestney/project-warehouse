import type {InboundOrderStatus} from "@/api/types.gen";

export const INBOUND_ORDER_STATUS_LABELS: Record<InboundOrderStatus, string> = {
  draft: "Черновик",
  processing: "В обработке",
  finished: "Завершён",
};

export const INBOUND_ORDER_STATUS_COLORS: Record<
  InboundOrderStatus,
  "default" | "warning" | "success"
> = {
  draft: "default",
  processing: "warning",
  finished: "success",
};
