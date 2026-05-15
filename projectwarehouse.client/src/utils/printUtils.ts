import type {BarcodeType} from "@/pages/PrintPage/BarcodeLabel.tsx";

export interface PrintItem {
  type: BarcodeType;
  value: string;
  label?: string;
}

export function openPrintPage(items: PrintItem[]): void {
  const params = new URLSearchParams();
  for (const item of items) {
    const raw = item.label ? `${item.value}|${item.label}` : item.value;
    params.append("item", `${item.type}:${raw}`);
  }
  window.open(`/print?${params.toString()}`, "_blank");
}
