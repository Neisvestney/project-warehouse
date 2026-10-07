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
  } catch {
    // storage blocked or over quota — the query string is the only channel left
    for (const item of items) {
      const raw = item.label ? `${item.value}|${item.label}` : item.value;
      params.append("item", `${item.type}:${raw}`);
    }
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

/** Mirrors the path check of the inline scheme script in index.html. */
export function isPrintPath(pathname: string): boolean {
  return pathname === "/print" || pathname.startsWith("/print/");
}

export function openStocktakeNodePrintPage(stocktakeId: string, nodeId: string): void {
  window.open(`/print/stocktakes/${stocktakeId}/nodes/${nodeId}`, "_blank");
}

const PRINT_RECEIPT_ITEMS_STORAGE_KEY = "print-receipt-items";

/** `itemIds` limits the sheet to those items; handed over like `openPrintPage`, `?item=` params as the fallback. */
export function openReceiptPrintPage(receiptId: string, itemIds?: string[]): void {
  const params = new URLSearchParams();
  if (itemIds) {
    try {
      sessionStorage.setItem(PRINT_RECEIPT_ITEMS_STORAGE_KEY, JSON.stringify(itemIds));
      params.set("from", PRINT_FROM_STORAGE);
    } catch {
      for (const id of itemIds) params.append("item", id);
    }
  }
  const query = params.size > 0 ? `?${params.toString()}` : "";
  window.open(`/print/receipts/${receiptId}${query}`, "_blank");
}

/** Item ids handed over by `openReceiptPrintPage`, or null when the whole receipt is printed. */
export function readReceiptPrintItemIds(params: URLSearchParams): Set<string> | null {
  if (params.get("from") === PRINT_FROM_STORAGE) {
    try {
      const parsed: unknown = JSON.parse(
        sessionStorage.getItem(PRINT_RECEIPT_ITEMS_STORAGE_KEY) ?? "null",
      );
      if (Array.isArray(parsed)) return new Set(parsed.filter((id) => typeof id === "string"));
    } catch {
      // unreadable storage prints the whole receipt
    }
    return null;
  }
  const ids = params.getAll("item");
  return ids.length > 0 ? new Set(ids) : null;
}
