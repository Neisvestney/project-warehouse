import type {ChipProps} from "@mui/material";
import type {
  MarketplaceCapabilities,
  MarketplaceCardMappingState,
  MarketplaceMappingSource,
  MarketplaceSyncScope,
  MarketplaceSyncStatus,
  MarketplaceType,
  MarketplaceWarehouseKind,
  MarketplaceWarehouseStatus,
} from "@/api/types.gen";

export const MARKETPLACE_TYPE_LABELS: Record<MarketplaceType, string> = {
  ozon: "Ozon",
  wildberries: "Wildberries",
};

export const MARKETPLACE_TYPE_COLORS: Record<MarketplaceType, "ozon" | "wb"> = {
  ozon: "ozon",
  wildberries: "wb",
};


export const SYNC_STATUS_LABELS: Record<MarketplaceSyncStatus, string> = {
  running: "Синхронизация",
  success: "Синхронизировано",
  failed: "Ошибка",
  canceled: "Отменено",
};

export const SYNC_SCOPE_LABELS: Record<MarketplaceSyncScope, string> = {
  warehouses: "Склады",
  cards: "Карточки",
  all: "Всё",
};

export const WAREHOUSE_KIND_LABELS: Record<MarketplaceWarehouseKind, string> = {
  unknown: "—",
  fbs: "FBS",
  rfbs: "rFBS",
  express: "Express",
  fbo: "FBO",
};

export const WAREHOUSE_STATUS_LABELS: Record<
  MarketplaceWarehouseStatus,
  {label: string; color: ChipProps["color"]}
> = {
  active: {label: "Активный", color: "success"},
  inactive: {label: "Не активный", color: "default"},
  unavailable: {label: "Недоступен", color: "warning"},
};

export const MAPPING_SOURCE_LABELS: Record<MarketplaceMappingSource, string> = {
  manual: "вручную",
  autoOfferId: "авто (артикул)",
  autoBarcode: "авто (штрихкод)",
};

export const MAPPING_STATE_LABELS: Record<MarketplaceCardMappingState, string> = {
  all: "Все",
  unmapped: "Не сопоставленные",
  mapped: "Сопоставленные",
  archivedItem: "Привязаны к архивному товару",
};

export const ALL_MAPPING_STATES: MarketplaceCardMappingState[] = [
  "all",
  "unmapped",
  "mapped",
  "archivedItem",
];

/**
 * Capabilities — это [Flags]-enum, и JsonStringEnumConverter шлёт комбинацию одной строкой
 * («warehouses, cards, sellerInfo»), чего сгенерированный union из одиночных значений не описывает.
 */
export function hasCapability(
  capabilities: MarketplaceCapabilities | null | undefined,
  flag: MarketplaceCapabilities,
): boolean {
  if (!capabilities) return false;
  return String(capabilities)
    .split(",")
    .map((part) => part.trim())
    .includes(flag);
}

export function formatDateTime(value: string | null | undefined): string {
  return value ? new Date(value).toLocaleString("ru-RU") : "—";
}

export function formatDuration(startedAt: string, finishedAt: string | null | undefined): string {
  if (!finishedAt) return "—";
  const seconds = Math.max(
    0,
    Math.round((new Date(finishedAt).getTime() - new Date(startedAt).getTime()) / 1000),
  );
  if (seconds < 60) return `${seconds} с`;
  const minutes = Math.floor(seconds / 60);
  if (minutes < 60) return `${minutes} мин ${seconds % 60} с`;
  return `${Math.floor(minutes / 60)} ч ${minutes % 60} мин`;
}

export function formatPrice(price: number | null | undefined, currency: string | null | undefined) {
  if (price == null) return "—";
  return `${price.toLocaleString("ru-RU")} ${currency ?? ""}`.trim();
}
