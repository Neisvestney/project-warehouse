import type {ReceiptReason, ReceiptStatus} from "@/api/types.gen";

export const RECEIPT_REASON_LABELS: Record<ReceiptReason, string> = {
  newGoods: "Новые товары",
  return: "Возврат",
  other: "Прочее",
};

export function formatReceiptNumber(n: number): string {
  return `ПРХ-${String(n).padStart(5, "0")}`;
}

export const RECEIPT_STATUS_LABELS: Record<ReceiptStatus, string> = {
  draft: "Черновик",
  planned: "Запланирована",
  processing: "Обрабатывается",
  finished: "Завершена",
  canceled: "Отменена",
};
