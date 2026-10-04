import type {BarcodeType} from "@/pages/PrintPage/BarcodeLabel.tsx";

export interface PrintItem {
  type: BarcodeType;
  value: string;
  label?: string;
}

/** `text` puts the label text first with a small code in the corner; the first label line is the title. */
export type PrintLayout = "barcode" | "text";

const PRINT_ITEMS_STORAGE_KEY = "print-page-items";
export const PRINT_FROM_STORAGE = "storage";

/**
 * Hands the labels over through sessionStorage: a large batch in the query string overflows the server's request
 * line limit. `window.open` copies sessionStorage into the new tab when it opens, so one fixed key never races
 * between batches and the print tab survives a reload. Falls back to `?item=` params when storage is unavailable.
 */
export function openPrintPage(items: PrintItem[], layout: PrintLayout = "barcode"): void {
  const params = new URLSearchParams();
  if (layout !== "barcode") params.set("layout", layout);
  try {
    sessionStorage.setItem(PRINT_ITEMS_STORAGE_KEY, JSON.stringify(items));
    params.set("from", PRINT_FROM_STORAGE);
    window.open(`/print?${params.toString()}`, "_blank");
    return;
  } catch {
    // storage blocked or over quota — the query string is the only channel left
  }
  for (const item of items) {
    const raw = item.label ? `${item.value}|${item.label}` : item.value;
    params.append("item", `${item.type}:${raw}`);
  }
  window.open(`/print?${params.toString()}`, "_blank");
}

/** Raw labels handed over by `openPrintPage`; the caller validates them. */
export function readStoredPrintItems(): unknown {
  try {
    const raw = sessionStorage.getItem(PRINT_ITEMS_STORAGE_KEY);
    return raw ? JSON.parse(raw) : null;
  } catch {
    return null;
  }
}

export function openStocktakeNodePrintPage(stocktakeId: string, nodeId: string): void {
  window.open(`/print/stocktakes/${stocktakeId}/nodes/${nodeId}`, "_blank");
}

export function openReceiptPrintPage(receiptId: string): void {
  window.open(`/print/receipts/${receiptId}`, "_blank");
}
